import { Component, computed, inject, input, ViewEncapsulation } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { ChatMessage } from '../../models/chat-message.models';
import { MarkdownRendererService } from '../../services/markdown-renderer.service';

@Component({
  selector: 'app-chat-message',
  standalone: true,
  imports: [MatChipsModule, MatExpansionModule],
  encapsulation: ViewEncapsulation.None,
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss'
})
export class ChatMessageComponent {
  private readonly markdownRenderer = inject(MarkdownRendererService);

  readonly message = input.required<ChatMessage>();
  protected readonly isAssistant = computed(() => this.message().role === 'assistant');
  protected readonly renderedAssistantContent = computed(() =>
    this.isAssistant() ? this.markdownRenderer.render(this.message().content) : '');

  protected documentName(sourcePath: string): string {
    return sourcePath.replaceAll('\\', '/').split('/').pop() || 'Knowledge document';
  }
}
