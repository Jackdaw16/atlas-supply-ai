import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TextFieldModule } from '@angular/cdk/text-field';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChatMessageComponent } from '../chat-message/chat-message.component';
import { ChatMessage, ChatPendingStatus } from '../../models/chat-message.models';
import { ChatService, ChatStreamEvent } from '../../services/chat.service';

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
  private readonly destroyRef = inject(DestroyRef);
  private messageId = 0;

  protected draft = '';
  protected readonly isPending = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly messages = signal<ChatMessage[]>([]);

  constructor() {
    this.destroyRef.onDestroy(() => this.chatService.abortActiveStream());
  }

  protected send(): void {
    const content = this.draft.trim();

    if (!content || this.isPending()) {
      return;
    }

    this.errorMessage.set(null);
    this.messages.update((messages) => [...messages, this.createMessage('user', content)]);
    this.draft = '';
    this.isPending.set(true);
    const pendingMessageId = this.appendPendingAssistantMessage();

    this.chatService.send({ message: content })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (event) => {
          this.handleStreamEvent(event, pendingMessageId);
        },
        error: (error: unknown) => {
          this.removeMessage(pendingMessageId);
          this.draft = content;
          this.errorMessage.set(this.toErrorMessage(error));
          this.isPending.set(false);
        }
      });
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

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The assistant service could not be reached. Check the API connection and try again.';
    }

    return 'The assistant could not complete this request. Your message is ready to retry.';
  }
}
