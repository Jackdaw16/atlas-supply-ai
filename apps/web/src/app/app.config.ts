import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { CHAT_API_CONFIG } from './core/services/chat-api.config';
import { DEMO_CONTACT_CONFIG } from './core/config/demo-contact.config';
import { authInterceptor } from './core/auth/auth.interceptor';

declare const API_BASE_URL: string | undefined;

const apiBaseUrl = typeof API_BASE_URL === 'string'
  ? API_BASE_URL.replace(/\/+$/, '')
  : '';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: CHAT_API_CONFIG, useValue: { baseUrl: apiBaseUrl } },
    { provide: DEMO_CONTACT_CONFIG, useValue: { demoContactEmail: 'demo@atlassupply.ai' } }
  ]
};
