import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TextFieldModule } from '@angular/cdk/text-field';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../../core/auth/auth.service';
import { CHAT_HISTORY_STORAGE_KEY_PREFIX } from '../../../../core/auth/auth-storage-keys';
import { ChatMessageComponent } from '../chat-message/chat-message.component';
import { ChatHistoryMessage, ChatMessage, ChatPendingStatus } from '../../models/chat-message.models';
import { ChatService, ChatStreamEvent } from '../../services/chat.service';

const MAX_HISTORY_MESSAGES = 10;
const MAX_HISTORY_CONTENT_BYTES = 16 * 1024;

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    TextFieldModule,
    ChatMessageComponent
  ],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent {
  private readonly chatService = inject(ChatService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private messageId = 0;
  private activeUsername: string | null = null;
  private retryMessageId: number | null = null;
  private streamGeneration = 0;

  protected draft = '';
  protected readonly isPending = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly messages = signal<ChatMessage[]>([]);

  constructor() {
    this.destroyRef.onDestroy(() => this.chatService.abortActiveStream());
    effect(() => {
      const username = this.authService.session() ? this.authService.username : null;
      this.syncHistory(username);
    });
  }

  protected send(): void {
    const content = this.draft.trim();

    if (!content || this.isPending()) {
      return;
    }

    this.errorMessage.set(null);
    const retryMessage = this.retryMessageId === null
      ? undefined
      : this.messages().find((message) => message.id === this.retryMessageId && message.role === 'user' && message.content === content);
    const history = this.trimHistory(this.toHistory(retryMessage?.id));
    if (!retryMessage) {
      this.messages.update((messages) => [...messages, this.createMessage('user', content)]);
    }
    this.retryMessageId = null;
    this.persistHistory();
    this.draft = '';
    this.isPending.set(true);
    const pendingMessageId = this.appendPendingAssistantMessage();
    const streamGeneration = this.streamGeneration;

    this.chatService.send({ message: content, history })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (event) => {
          if (streamGeneration !== this.streamGeneration) {
            return;
          }
          this.handleStreamEvent(event, pendingMessageId);
        },
        error: (error: unknown) => {
          if (streamGeneration !== this.streamGeneration) {
            return;
          }
          this.removeMessage(pendingMessageId);
          this.retryMessageId = retryMessage?.id ?? this.messages().at(-1)?.id ?? null;
          this.draft = content;
          this.errorMessage.set(this.toErrorMessage(error));
          this.isPending.set(false);
        }
      });
  }

  protected startNewConversation(): void {
    if (this.messages().length > 0 && !globalThis.confirm('Start a new conversation? The current conversation will be cleared.')) {
      return;
    }

    this.streamGeneration += 1;
    this.chatService.abortActiveStream();
    this.removeStoredHistory(this.authService.username);
    this.messageId = 0;
    this.retryMessageId = null;
    this.draft = '';
    this.errorMessage.set(null);
    this.isPending.set(false);
    this.messages.set([]);
  }

  protected onDraftKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private createMessage(
    role: ChatMessage['role'],
    content: string,
    toolsUsed?: string[],
    ragSources?: ChatMessage['ragSources'],
    options?: Pick<ChatMessage, 'isPending' | 'pendingStatus'>
  ): ChatMessage {
    this.messageId += 1;
    return { id: this.messageId, role, content, toolsUsed, ragSources, ...options };
  }

  private appendPendingAssistantMessage(): number {
    const pendingMessage = this.createMessage('assistant', '', undefined, undefined, { isPending: true });
    this.messages.update((messages) => [...messages, pendingMessage]);
    return pendingMessage.id;
  }

  private handleStreamEvent(event: ChatStreamEvent, pendingMessageId: number): void {
    if (event.type === 'status') {
      this.updatePendingMessage(pendingMessageId, event.status);
      return;
    }

    this.messages.update((messages) => messages.map((message) => message.id === pendingMessageId
      ? {
        ...message,
        content: event.response.message,
        toolsUsed: event.response.toolsUsed,
        ragSources: event.response.ragSources,
        isPending: false,
        pendingStatus: undefined
      }
      : message));
    this.persistHistory();
    this.isPending.set(false);
  }

  private updatePendingMessage(messageId: number, pendingStatus: ChatPendingStatus): void {
    this.messages.update((messages) => messages.map((message) => message.id === messageId
      ? { ...message, isPending: true, pendingStatus }
      : message));
  }

  private removeMessage(messageId: number): void {
    this.messages.update((messages) => messages.filter((message) => message.id !== messageId));
  }

  private syncHistory(username: string | null): void {
    if (!username) {
      this.removeStoredHistory(this.activeUsername);
      this.activeUsername = null;
      this.retryMessageId = null;
      this.messages.set([]);
      return;
    }

    if (username === this.activeUsername) {
      return;
    }

    this.activeUsername = username;
    this.messageId = 0;
    this.retryMessageId = null;
    this.messages.set(this.readHistory(username).map((message) => this.createMessage(message.role, message.content)));
  }

  private persistHistory(): void {
    const username = this.authService.username;
    if (!username) {
      this.removeStoredHistory(this.activeUsername);
      this.activeUsername = null;
      return;
    }

    const history = this.trimHistory(this.toHistory());
    const storage = this.getStorage();
    if (!storage) {
      return;
    }

    try {
      if (history.length === 0) {
        storage.removeItem(this.storageKey(username));
      } else {
        storage.setItem(this.storageKey(username), JSON.stringify(history));
      }
    } catch {
      // Storage may be unavailable or full; the chat remains usable without persistence.
    }
  }

  private readHistory(username: string): ChatHistoryMessage[] {
    const storage = this.getStorage();
    if (!storage) {
      return [];
    }

    const key = this.storageKey(username);
    let serialized: string | null;
    try {
      serialized = storage.getItem(key);
    } catch {
      return [];
    }

    if (!serialized) {
      return [];
    }

    try {
      const parsed: unknown = JSON.parse(serialized);
      if (!Array.isArray(parsed)) {
        throw new Error('Chat history must be an array.');
      }

      const history = this.trimHistory(parsed.filter(this.isHistoryMessage));
      if (history.length === 0) {
        storage.removeItem(key);
      } else if (history.length !== parsed.length) {
        storage.setItem(key, JSON.stringify(history));
      }
      return history;
    } catch {
      try {
        storage.removeItem(key);
      } catch {
        // Ignore unavailable storage while recovering from invalid persisted data.
      }
      return [];
    }
  }

  private toHistory(excludedMessageId?: number): ChatHistoryMessage[] {
    return this.messages()
      .filter((message) => !message.isPending && message.id !== excludedMessageId)
      .map(({ role, content }) => ({ role, content }));
  }

  private trimHistory(history: ChatHistoryMessage[]): ChatHistoryMessage[] {
    const normalized = history.filter((message) => this.isHistoryMessage(message));
    const trimmed = normalized.slice(-MAX_HISTORY_MESSAGES);
    let contentBytes = trimmed.reduce((total, message) => total + this.utf8ByteLength(message.content), 0);

    while (trimmed.length > 0 && contentBytes > MAX_HISTORY_CONTENT_BYTES) {
      contentBytes -= this.utf8ByteLength(trimmed.shift()!.content);
    }

    return trimmed;
  }

  private isHistoryMessage(value: unknown): value is ChatHistoryMessage {
    if (!value || typeof value !== 'object') {
      return false;
    }

    const { role, content } = value as Partial<ChatHistoryMessage>;
    return (role === 'user' || role === 'assistant') && typeof content === 'string';
  }

  private utf8ByteLength(content: string): number {
    return typeof TextEncoder === 'undefined' ? Number.POSITIVE_INFINITY : new TextEncoder().encode(content).byteLength;
  }

  private removeStoredHistory(username: string | null): void {
    const storage = this.getStorage();
    if (!storage || !username) {
      return;
    }

    try {
      storage.removeItem(this.storageKey(username));
    } catch {
      // Ignore unavailable storage during logout or session expiry.
    }
  }

  private storageKey(username: string): string {
    return `${CHAT_HISTORY_STORAGE_KEY_PREFIX}${encodeURIComponent(username)}`;
  }

  private getStorage(): Storage | null {
    try {
      return typeof sessionStorage === 'undefined' ? null : sessionStorage;
    } catch {
      return null;
    }
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The assistant service could not be reached. Check the API connection and try again.';
    }

    return 'The assistant could not complete this request. Your message is ready to retry.';
  }
}
