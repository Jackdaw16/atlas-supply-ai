import { HttpClient } from '@angular/common/http';
import { DestroyRef, inject, Injectable, NgZone, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { CHAT_API_CONFIG } from '../services/chat-api.config';
import { AuthSession, LoginRequest } from './auth.models';
import { AUTH_SESSION_STORAGE_KEY, CHAT_HISTORY_STORAGE_KEY_PREFIX } from './auth-storage-keys';

export { AUTH_SESSION_STORAGE_KEY } from './auth-storage-keys';
const MAX_TIMER_DELAY_MS = 2_147_483_647;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiConfig = inject(CHAT_API_CONFIG);
  private readonly destroyRef = inject(DestroyRef);
  private readonly zone = inject(NgZone);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly sessionState = signal<AuthSession | null>(this.readSession());

  readonly session = this.sessionState.asReadonly();

  constructor() {
    this.destroyRef.onDestroy(() => this.clearExpiryTimer());
    this.scheduleExpiry(this.sessionState());
  }

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
      this.handleUnauthorized();
      return null;
    }

    return session;
  }

  private persistSession(session: AuthSession): void {
    if (!this.isValidSession(session) || this.isExpired(session)) {
      this.handleUnauthorized();
      return;
    }

    const normalizedSession: AuthSession = {
      accessToken: session.accessToken,
      expiresAtUtc: session.expiresAtUtc,
      username: session.username,
      scopes: [...session.scopes]
    };

    this.getStorage()?.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(normalizedSession));
    this.sessionState.set(normalizedSession);
    this.scheduleExpiry(normalizedSession);
  }

  private clearSession(): void {
    this.clearExpiryTimer();
    this.clearStoredSession(this.sessionState());
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
        this.clearStoredSession(session);
        return null;
      }

      return { ...session, scopes: [...session.scopes] };
    } catch {
      this.getStorage()?.removeItem(AUTH_SESSION_STORAGE_KEY);
      return null;
    }
  }

  private clearStoredSession(session: Partial<AuthSession> | null): void {
    const storage = this.getStorage();
    if (!storage) {
      return;
    }

    if (session && this.isValidSession(session)) {
      storage.removeItem(`${CHAT_HISTORY_STORAGE_KEY_PREFIX}${encodeURIComponent(session.username)}`);
    }

    storage.removeItem(AUTH_SESSION_STORAGE_KEY);
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

  private scheduleExpiry(session: AuthSession | null): void {
    this.clearExpiryTimer();
    if (!session) {
      return;
    }

    const expiresAt = Date.parse(session.expiresAtUtc);
    if (Number.isNaN(expiresAt) || expiresAt <= Date.now()) {
      this.handleUnauthorized();
      return;
    }

    const delay = Math.min(expiresAt - Date.now(), MAX_TIMER_DELAY_MS);
    this.expiryTimer = this.zone.runOutsideAngular(() => setTimeout(() => {
      this.expiryTimer = undefined;
      this.zone.run(() => {
        if (this.sessionState() !== session) {
          return;
        }

        if (this.isExpired(session)) {
          this.handleUnauthorized();
          return;
        }

        this.scheduleExpiry(session);
      });
    }, delay));
  }

  private clearExpiryTimer(): void {
    if (this.expiryTimer !== undefined) {
      clearTimeout(this.expiryTimer);
      this.expiryTimer = undefined;
    }
  }

  private getStorage(): Storage | null {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage;
  }
}
