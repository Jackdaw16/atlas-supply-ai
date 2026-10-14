import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WebMcpRegistryService } from './webmcp-registry.service';

@Component({
  selector: 'app-webmcp-diagnostics-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './webmcp-diagnostics-panel.component.html',
  styleUrl: './webmcp-diagnostics-panel.component.scss'
})
export class WebMcpDiagnosticsPanelComponent {
  protected readonly registry = inject(WebMcpRegistryService);
}
