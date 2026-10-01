import { MediaMatcher } from '@angular/cdk/layout';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatSidenavModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  protected readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly mediaMatcher = inject(MediaMatcher);
  private readonly handsetQuery = this.mediaMatcher.matchMedia('(max-width: 767px)');
  protected readonly isHandset = signal(this.handsetQuery.matches);
  protected readonly navigationOpen = signal(!this.handsetQuery.matches);

  constructor() {
    const updateHandsetState = (event: MediaQueryListEvent) => {
      this.isHandset.set(event.matches);
      this.navigationOpen.set(!event.matches);
    };

    this.handsetQuery.addEventListener('change', updateHandsetState);
    this.destroyRef.onDestroy(() => this.handsetQuery.removeEventListener('change', updateHandsetState));
  }

  protected toggleNavigation(): void {
    if (this.isHandset()) {
      this.navigationOpen.update((isOpen) => !isOpen);
    }
  }

  protected closeNavigationOnHandset(): void {
    if (this.isHandset()) {
      this.navigationOpen.set(false);
    }
  }
}
