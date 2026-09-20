import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { MarkdownRendererService } from './markdown-renderer.service';

describe('MarkdownRendererService', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('renders supported Markdown formatting for assistant content', () => {
    const renderer = TestBed.inject(MarkdownRendererService);

    const html = renderer.render([
      '**Bold** and *italic* with `inline code`.',
      '',
      '- First item',
      '- Second item',
      '',
      '```json',
      '{"status":"ok"}',
      '```',
      '',
      '> Operational note',
      '',
      '| Status | Count |',
      '| --- | ---: |',
      '| Delayed | 2 |'
    ].join('\n'));

    expect(html).toContain('<strong>Bold</strong>');
    expect(html).toContain('<em>italic</em>');
    expect(html).toContain('<code>inline code</code>');
    expect(html).toContain('<ul>');
    expect(html).toContain('<pre><code>');
    expect(html).toContain('<blockquote>');
    expect(html).toContain('<table>');
  });

  it('removes unsafe HTML and URL protocols from untrusted content', () => {
    const renderer = TestBed.inject(MarkdownRendererService);

    const html = renderer.render('<script>alert(1)</script><a href="javascript:alert(1)" onclick="alert(1)">Unsafe</a>');

    expect(html).not.toContain('<script');
    expect(html).not.toContain('javascript:');
    expect(html).not.toContain('onclick');
  });
});
