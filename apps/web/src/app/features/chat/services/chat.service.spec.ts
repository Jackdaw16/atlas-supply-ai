import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { AUTH_SESSION_STORAGE_KEY } from '../../../core/auth/auth.service';
import { ChatService } from './chat.service';

describe('ChatService SSE streaming', () => {
  const activeSession = {
    accessToken: 'test-access-token',
    expiresAtUtc: '2030-01-01T00:00:00.000Z',
    username: 'operator-user',
    scopes: ['agent.chat']
  };

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
    sessionStorage.clear();
  });

  it('streams fragmented SSE events and maps the completed payload back to the chat response shape', async () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const fetchMock = vi.fn().mockResolvedValue(createStreamResponse([
      'event: processing\ndata: {}\n\n' +
      'event: tool_started\ndata: {"ToolCallId":"tool-1","ToolName":"search_orders"}\n\n' +
      'event: rag_started\ndata: {"ToolCallId":"rag-1","Query":"delayed orders","TopK":5}\n\n',
      'event: rag_finished\ndata: {"ToolCallId":"rag-1","Query":"delayed orders","TopK":5,"Sources":[]}\n\n' +
      'event: tool_finished\ndata: {"ToolCallId":"tool-1","ToolName":"search_orders","IsError":false}\n\n',
      'event: completed\ndata: {"Message":"Done","ToolsUsed":["search_orders"],"RagSources":[{"SourcePath":"docs/knowledge/orders.md","Heading":"Delayed orders"}]}\n\n'
    ]));
    vi.stubGlobal('fetch', fetchMock);

    const { service } = configureTestBed();

    const events = await collectStreamEvents(service);

    expect(fetchMock).toHaveBeenCalledWith('https://api.atlas.test/api/chat/stream', expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        Accept: 'text/event-stream',
        Authorization: `Bearer ${activeSession.accessToken}`,
        'Content-Type': 'application/json'
      }),
      body: JSON.stringify({ message: 'Which supplier orders are delayed?' })
    }));
    expect(events).toEqual([
      { type: 'status', status: 'processing' },
      { type: 'status', status: 'tool' },
      { type: 'status', status: 'rag' },
      { type: 'status', status: 'waiting' },
      { type: 'status', status: 'waiting' },
      {
        type: 'completed',
        response: {
          message: 'Done',
          toolsUsed: ['search_orders'],
          ragSources: [{ sourcePath: 'docs/knowledge/orders.md', heading: 'Delayed orders' }]
        }
      }
    ]);
  });

  it('aborts the active SSE request when a new send starts', async () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const firstAbortSignal = { current: undefined as AbortSignal | undefined };
    const releaseFirstFetch = createDeferred<Response>();
    const fetchMock = vi.fn()
      .mockImplementationOnce((_input: RequestInfo | URL, init?: RequestInit) => {
        firstAbortSignal.current = init?.signal ?? undefined;
        return releaseFirstFetch.promise;
      })
      .mockResolvedValueOnce(createStreamResponse([
        'event: completed\ndata: {"Message":"Second response","ToolsUsed":[],"RagSources":[]}\n\n'
      ]));
    vi.stubGlobal('fetch', fetchMock);

    const { service } = configureTestBed();

    const firstSubscription = service.send({ message: 'First question' }).subscribe({ error: () => undefined });
    await Promise.resolve();
    service.send({ message: 'Second question' }).subscribe();
    await Promise.resolve();

    expect(firstAbortSignal.current?.aborted).toBe(true);
    firstSubscription.unsubscribe();
    releaseFirstFetch.resolve(createStreamResponse(['event: completed\ndata: {"Message":"Ignored","ToolsUsed":[],"RagSources":[]}\n\n']));
  });
});

function configureTestBed() {
  const router = {
    navigateByUrl: vi.fn().mockResolvedValue(true)
  };

  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      { provide: Router, useValue: router },
      { provide: CHAT_API_CONFIG, useValue: { baseUrl: 'https://api.atlas.test' } }
    ]
  });

  return {
    service: TestBed.inject(ChatService),
    router
  };
}

function collectStreamEvents(service: ChatService): Promise<unknown[]> {
  return new Promise((resolve, reject) => {
    const events: unknown[] = [];
    service.send({ message: 'Which supplier orders are delayed?' }).subscribe({
      next: (event) => events.push(event),
      error: reject,
      complete: () => resolve(events)
    });
  });
}

function createStreamResponse(chunks: string[]): Response {
  return new Response(new ReadableStream<Uint8Array>({
    start(controller) {
      const encoder = new TextEncoder();
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
      }
      controller.close();
    }
  }), {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' }
  });
}

function createDeferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}
