import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { CHAT_API_CONFIG } from '../services/chat-api.config';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const apiConfig = inject(CHAT_API_CONFIG);
  const loginUrl = `${apiConfig.baseUrl}/api/auth/login`;

  if (!isApiRequest(request.url, apiConfig.baseUrl) || isLoginRequest(request.url, loginUrl)) {
    return next(request);
  }

  const accessToken = authService.accessToken;
  if (!accessToken) {
    return next(request);
  }

  const authenticatedRequest = request.clone({
    setHeaders: { Authorization: `Bearer ${accessToken}` }
  });

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        authService.handleUnauthorized();
      }

      return throwError(() => error);
    })
  );
};

function isApiRequest(url: string, baseUrl: string): boolean {
  return url.startsWith(`${baseUrl}/api/`);
}

function isLoginRequest(url: string, loginUrl: string): boolean {
  return url === loginUrl || url.startsWith(`${loginUrl}?`);
}
