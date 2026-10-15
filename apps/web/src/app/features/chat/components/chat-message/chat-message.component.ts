import { Component, computed, inject, input, ViewEncapsulation } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { ChatPendingStatus } from '../../models/chat-message.models';
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
  private readonly pendingStatusLabels: Record<ChatPendingStatus, string> = {
    processing: 'procesando',
    tool: 'ejecutando herramienta',
    rag: 'buscando documentación RAG',
    waiting: 'esperando respuesta'
  };

  readonly message = input.required<ChatMessage>();
  protected readonly isAssistant = computed(() => this.message().role === 'assistant');
  protected readonly isPendingAssistant = computed(() => this.isAssistant() && this.message().isPending === true);
  protected readonly pendingStatusLabel = computed(() => {
    const pendingStatus = this.message().pendingStatus;
    return pendingStatus ? this.pendingStatusLabels[pendingStatus] : null;
  });
  protected readonly renderedAssistantContent = computed(() =>
    this.isAssistant() && this.message().content ? this.markdownRenderer.render(this.message().content) : '');

  protected documentName(sourcePath: string): string {
    return sourcePath.replaceAll('\\', '/').split('/').pop() || 'Knowledge document';
  }
}
