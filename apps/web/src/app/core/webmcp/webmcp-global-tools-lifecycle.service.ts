import { effect, EffectRef, inject, Injectable, Injector } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { WebMcpToolsService } from './webmcp-tools.service';

@Injectable({ providedIn: 'root' })
export class WebMcpGlobalToolsLifecycleService {
  private readonly authService = inject(AuthService);
  private readonly injector = inject(Injector);
  private readonly tools = inject(WebMcpToolsService);
  private lifecycleEffect?: EffectRef;

  start(): void {
    if (this.lifecycleEffect) {
      return;
    }

    this.lifecycleEffect = effect((onCleanup) => {
      this.authService.session();
      if (!this.authService.hasValidSession()) {
        return;
      }

      const registration = this.tools.registerGlobalReadTools();
      onCleanup(() => registration.dispose());
    }, { injector: this.injector });
  }
}
