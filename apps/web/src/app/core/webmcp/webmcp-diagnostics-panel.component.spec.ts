import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { WebMcpDiagnosticsPanelComponent } from './webmcp-diagnostics-panel.component';
import { WebMcpRegistryService } from './webmcp-registry.service';

describe('WebMcpDiagnosticsPanelComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('renders the supplied runtime diagnostics state', () => {
    TestBed.configureTestingModule({
      imports: [WebMcpDiagnosticsPanelComponent],
      providers: [{
        provide: WebMcpRegistryService,
        useValue: {
          diagnostics: signal({
            webMcpApiAvailable: true,
            modelContextAvailable: true,
            registeredToolNames: ['search_orders'],
            registrationErrors: [{ toolName: 'search_incidents', message: 'registration failed' }]
          })
        }
      }]
    });

    const fixture = TestBed.createComponent(WebMcpDiagnosticsPanelComponent);
    fixture.detectChanges();

    const panel = fixture.nativeElement as HTMLElement;
    expect(panel.textContent).toContain('WEBMCP DIAGNOSTICS');
    expect(panel.textContent).toContain('WebMCP API available');
    expect(panel.textContent).toContain('YES');
    expect(panel.textContent).toContain('AtlasSupply tools registered 1');
    expect(panel.textContent).toContain('✓search_orders');
    expect(panel.textContent).toContain('search_incidents');
    expect(panel.textContent).toContain('registration failed');
  });

  it('explains when document.modelContext is unavailable', () => {
    TestBed.configureTestingModule({
      imports: [WebMcpDiagnosticsPanelComponent],
      providers: [{
        provide: WebMcpRegistryService,
        useValue: {
          diagnostics: signal({
            webMcpApiAvailable: false,
            modelContextAvailable: false,
            registeredToolNames: [],
            registrationErrors: []
          })
        }
      }]
    });

    const fixture = TestBed.createComponent(WebMcpDiagnosticsPanelComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Reason: document.modelContext is undefined'
    );
  });
});
