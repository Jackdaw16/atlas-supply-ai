import { Injectable } from '@angular/core';
import DOMPurify from 'dompurify';
import { marked } from 'marked';

@Injectable({ providedIn: 'root' })
export class MarkdownRendererService {
  render(markdown: string): string {
    const renderedHtml = marked.parse(markdown, { async: false, breaks: false, gfm: true });

    return DOMPurify.sanitize(renderedHtml, {
      ALLOWED_ATTR: ['href', 'title'],
      ALLOWED_TAGS: [
        'a', 'blockquote', 'br', 'code', 'del', 'em', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
        'hr', 'li', 'ol', 'p', 'pre', 's', 'strong', 'table', 'tbody', 'td', 'th', 'thead',
        'tr', 'ul'
      ],
      ALLOWED_URI_REGEXP: /^(?:(?:https?|mailto):|\/|#)/i
    });
  }
}
