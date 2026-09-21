import { Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { DEMO_CONTACT_CONFIG } from '../../../../core/config/demo-contact.config';

const demoRequestSubject = 'Solicitud de acceso a Atlas Supply AI';
const demoRequestBody = `Hola,

Me gustaría solicitar acceso a la demo de Atlas Supply AI.

Nombre:
Empresa:
Cargo:
Motivo de la solicitud:

Gracias.`;

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly demoContactConfig = inject(DEMO_CONTACT_CONFIG);

  protected username = '';
  protected password = '';
  protected readonly demoRequestMailto = `mailto:${this.demoContactConfig.demoContactEmail}?subject=${encodeURIComponent(demoRequestSubject)}&body=${encodeURIComponent(demoRequestBody)}`;
  protected readonly isPasswordVisible = signal(false);
  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected signIn(form: NgForm): void {
    if (form.invalid || this.isSubmitting()) {
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);
    this.authService.login({ username: this.username.trim(), password: this.password }).subscribe({
      next: () => {
        void this.router.navigateByUrl('/chat');
      },
      error: () => {
        this.errorMessage.set('Invalid username or password.');
        this.isSubmitting.set(false);
      }
    });
  }

  protected togglePasswordVisibility(): void {
    this.isPasswordVisible.update((isVisible) => !isVisible);
  }
}
