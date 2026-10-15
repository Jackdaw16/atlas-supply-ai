import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, Subscriber } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { ChatRequest, ChatResponse } from '../models/chat-api.models';
import { ChatPendingStatus } from '../models/chat-message.models';

interface SseEventRecord {
  event: string;
  data: string;
}

export type ChatStreamEvent =
  | { type: 'status'; status: ChatPendingStatus }
  | { type: 'completed'; response: ChatResponse };

interface SseCompletedPayload {
  Message: string;
  ToolsUsed: string[];
  RagSources: Array<{ SourcePath: string; Heading: string | null }>;
}

interface SseToolProgressPayload {
  ToolCallId: string;
  ToolName: string;
}

interface SseRagProgressPayload {
  ToolCallId: string;
  Query: string;
  TopK: number;
}

interface SseErrorPayload {
  message?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly authService = inject(AuthService);
  private readonly apiConfig = inject(CHAT_API_CONFIG);
  private activeStreamAbortController?: AbortController;

  send(request: ChatRequest): Observable<ChatStreamEvent> {
    return new Observable<ChatStreamEvent>((subscriber) => {
      this.abortActiveStream();

      const url = `${this.apiConfig.baseUrl}/api/chat/stream`;
      const accessToken = this.authService.accessToken;
      if (!accessToken) {
        this.authService.handleUnauthorized();
        subscriber.error(new HttpErrorResponse({ status: 401, statusText: 'Unauthorized', url }));
        return undefined;
      }

      const abortController = new AbortController();
      this.activeStreamAbortController = abortController;

      void this.streamChat(url, request, accessToken, abortController, subscriber);

      return () => {
        if (this.activeStreamAbortController === abortController) {
          this.activeStreamAbortController = undefined;
        }
        abortController.abort();
      };
    });
  }

  abortActiveStream(): void {
    this.activeStreamAbortController?.abort();
    this.activeStreamAbortController = undefined;
  }

  private async streamChat(
    url: string,
    request: ChatRequest,
    accessToken: string,
    abortController: AbortController,
    subscriber: Subscriber<ChatStreamEvent>
  ): Promise<void> {
    try {
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          Accept: 'text/event-stream',
          Authorization: `Bearer ${accessToken}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(request),
        signal: abortController.signal
      });

      if (response.status === 401) {
        this.authService.handleUnauthorized();
      }

      if (!response.ok) {
        throw new HttpErrorResponse({
          status: response.status,
          statusText: response.statusText,
          url
        });
      }

      if (!response.body) {
        throw new Error('The assistant stream returned no response body.');
      }

      await this.consumeEventStream(response.body, subscriber);
    } catch (error) {
      if (this.isAbortError(error)) {
        subscriber.complete();
        return;
      }

      subscriber.error(this.normalizeError(error, url));
    } finally {
      if (this.activeStreamAbortController === abortController) {
        this.activeStreamAbortController = undefined;
      }
    }
  }

  private async consumeEventStream(
    stream: ReadableStream<Uint8Array>,
    subscriber: Subscriber<ChatStreamEvent>
  ): Promise<void> {
    const reader = stream.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let isTerminalEventReceived = false;

    try {
      while (!isTerminalEventReceived) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const parsed = this.parseEventBuffer(buffer);
        buffer = parsed.remainder;

        for (const event of parsed.events) {
          isTerminalEventReceived = this.handleStreamEvent(event, subscriber);
          if (isTerminalEventReceived) {
            break;
          }
        }
      }

      if (!isTerminalEventReceived) {
        buffer += decoder.decode();
        const parsed = this.parseEventBuffer(buffer, true);

        for (const event of parsed.events) {
          isTerminalEventReceived = this.handleStreamEvent(event, subscriber);
          if (isTerminalEventReceived) {
            break;
          }
        }
      }

      if (!isTerminalEventReceived) {
        throw new Error('The assistant stream ended before the response was completed.');
      }
    } finally {
      reader.releaseLock();
    }
  }

  private parseEventBuffer(buffer: string, flush = false): { events: SseEventRecord[]; remainder: string } {
    const normalizedBuffer = buffer.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    const segments = normalizedBuffer.split('\n\n');
    const hasTrailingSeparator = normalizedBuffer.endsWith('\n\n');
    const trailingSegment = !flush && !hasTrailingSeparator ? segments.pop() ?? '' : '';
    const events = segments
      .map((segment) => this.parseEventSegment(segment))
      .filter((event): event is SseEventRecord => event !== null);

    if (!flush) {
      return { events, remainder: trailingSegment };
    }

    const trailingEvent = trailingSegment.trim() ? this.parseEventSegment(trailingSegment) : null;
    return {
      events: trailingEvent ? [...events, trailingEvent] : events,
      remainder: ''
    };
  }

  private parseEventSegment(segment: string): SseEventRecord | null {
    const lines = segment.split('\n');
    let eventName = '';
    const dataLines: string[] = [];

    for (const line of lines) {
      if (!line || line.startsWith(':')) {
        continue;
      }

      const separatorIndex = line.indexOf(':');
      const field = separatorIndex >= 0 ? line.slice(0, separatorIndex) : line;
      const rawValue = separatorIndex >= 0 ? line.slice(separatorIndex + 1) : '';
      const value = rawValue.startsWith(' ') ? rawValue.slice(1) : rawValue;

      if (field === 'event') {
        eventName = value;
        continue;
      }

      if (field === 'data') {
        dataLines.push(value);
      }
    }

    if (!eventName || dataLines.length === 0) {
      return null;
    }

    return {
      event: eventName,
      data: dataLines.join('\n')
    };
  }

  private handleStreamEvent(
    event: SseEventRecord,
    subscriber: Subscriber<ChatStreamEvent>
  ): boolean {
    switch (event.event) {
      case 'processing':
        subscriber.next({ type: 'status', status: 'processing' });
        return false;
      case 'tool_started':
        this.parseJson<SseToolProgressPayload>(event.data);
        subscriber.next({ type: 'status', status: 'tool' });
        return false;
      case 'rag_started':
        this.parseJson<SseRagProgressPayload>(event.data);
        subscriber.next({ type: 'status', status: 'rag' });
        return false;
      case 'tool_finished':
        this.parseJson<SseToolProgressPayload>(event.data);
        subscriber.next({ type: 'status', status: 'waiting' });
        return false;
      case 'rag_finished':
        this.parseJson<SseRagProgressPayload>(event.data);
        subscriber.next({ type: 'status', status: 'waiting' });
        return false;
      case 'completed':
        subscriber.next({ type: 'completed', response: this.toChatResponse(this.parseJson<SseCompletedPayload>(event.data)) });
        subscriber.complete();
        return true;
      case 'error': {
        const payload = this.parseJson<SseErrorPayload>(event.data);
        throw new Error(payload.message?.trim() || 'The assistant could not complete this request.');
      }
      default:
        return false;
    }
  }

  private toChatResponse(payload: SseCompletedPayload): ChatResponse {
    return {
      message: payload.Message,
      toolsUsed: payload.ToolsUsed,
      ragSources: payload.RagSources.map((source) => ({
        sourcePath: source.SourcePath,
        heading: source.Heading
      }))
    };
  }

  private parseJson<T>(value: string): T {
    return JSON.parse(value) as T;
  }

  private normalizeError(error: unknown, url: string): unknown {
    if (error instanceof HttpErrorResponse || error instanceof Error) {
      return error;
    }

    if (this.isAbortError(error)) {
      return new HttpErrorResponse({ status: 0, statusText: 'AbortError', url });
    }

    return new HttpErrorResponse({ status: 0, statusText: 'NetworkError', url, error });
  }

  private isAbortError(error: unknown): boolean {
    return error instanceof DOMException
      ? error.name === 'AbortError'
      : error instanceof Error && error.name === 'AbortError';
  }
}
