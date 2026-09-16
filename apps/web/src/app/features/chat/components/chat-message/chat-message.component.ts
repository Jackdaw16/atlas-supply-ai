import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { ChatMessage } from '../../models/chat-message.models';

@Component({
  selector: 'app-chat-message',
  standalone: true,
  imports: [MatChipsModule, MatExpansionModule],
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss'
})
export class ChatMessageComponent {
  readonly message = input.required<ChatMessage>();
  protected readonly isAssistant = computed(() => this.message().role === 'assistant');

  protected documentName(sourcePath: string): string {
    return sourcePath.replaceAll('\\', '/').split('/').pop() || 'Knowledge document';
  }
}
