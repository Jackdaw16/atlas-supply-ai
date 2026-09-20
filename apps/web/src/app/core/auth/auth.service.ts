import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { CHAT_API_CONFIG } from '../services/chat-api.config';
import { AuthSession, LoginRequest } from './auth.models';

export const AUTH_SESSION_STORAGE_KEY = 'atlas-supply.auth-session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiConfig = inject(CHAT_API_CONFIG);
  private readonly sessionState = signal<AuthSession | null>(this.readSession());

  readonly session = this.sessionState.asReadonly();

  get accessToken(): string | null {
    return this.getValidSession()?.accessToken ?? null;
  }

  get expiresAtUtc(): string | null {
    return this.getValidSession()?.expiresAtUtc ?? null;
  }

  get username(): string | null {
    return this.getValidSession()?.username ?? null;
  }

  get scopes(): readonly string[] {
    return this.getValidSession()?.scopes ?? [];
  }

  login(request: LoginRequest) {
    return this.http.post<AuthSession>(`${this.apiConfig.baseUrl}/api/auth/login`, request).pipe(
      tap((session) => this.persistSession(session))
    );
  }

  logout(): void {
    this.clearSession();
    void this.router.navigateByUrl('/login');
  }

  handleUnauthorized(): void {
    this.clearSession();
    void this.router.navigateByUrl('/login');
  }

  hasValidSession(): boolean {
    return this.getValidSession() !== null;
  }

  private getValidSession(): AuthSession | null {
    const session = this.sessionState();

    if (session && this.isExpired(session)) {
      this.clearSession();
      return null;
    }

    return session;
  }

  private persistSession(session: AuthSession): void {
    const normalizedSession: AuthSession = {
      accessToken: session.accessToken,
      expiresAtUtc: session.expiresAtUtc,
      username: session.username,
      scopes: [...session.scopes]
    };

    this.getStorage()?.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(normalizedSession));
    this.sessionState.set(normalizedSession);
  }

  private clearSession(): void {
    this.getStorage()?.removeItem(AUTH_SESSION_STORAGE_KEY);
    this.sessionState.set(null);
  }

  private readSession(): AuthSession | null {
    const serializedSession = this.getStorage()?.getItem(AUTH_SESSION_STORAGE_KEY);
    if (!serializedSession) {
      return null;
    }

    try {
      const session = JSON.parse(serializedSession) as Partial<AuthSession>;
      if (!this.isValidSession(session) || this.isExpired(session)) {
        this.getStorage()?.removeItem(AUTH_SESSION_STORAGE_KEY);
        return null;
      }

      return { ...session, scopes: [...session.scopes] };
    } catch {
      this.getStorage()?.removeItem(AUTH_SESSION_STORAGE_KEY);
      return null;
    }
  }

  private isValidSession(session: Partial<AuthSession>): session is AuthSession {
    return typeof session.accessToken === 'string' &&
      typeof session.expiresAtUtc === 'string' &&
      typeof session.username === 'string' &&
      Array.isArray(session.scopes) &&
      session.scopes.every((scope) => typeof scope === 'string');
  }

  private isExpired(session: AuthSession): boolean {
    const expiresAt = Date.parse(session.expiresAtUtc);
    return Number.isNaN(expiresAt) || expiresAt <= Date.now();
  }

  private getStorage(): Storage | null {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage;
  }
}
