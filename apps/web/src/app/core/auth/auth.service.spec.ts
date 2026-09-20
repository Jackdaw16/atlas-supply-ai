import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CHAT_API_CONFIG } from '../services/chat-api.config';
import { authGuard } from './auth.guard';
import { authInterceptor } from './auth.interceptor';
import { AuthSession } from './auth.models';
import { AuthService, AUTH_SESSION_STORAGE_KEY } from './auth.service';

describe('AuthService and authentication HTTP integration', () => {
  const activeSession: AuthSession = {
    accessToken: 'test-access-token',
    expiresAtUtc: '2030-01-01T00:00:00.000Z',
    username: 'operator-user',
    scopes: ['agent.incidents.create']
  };

  afterEach(() => {
    TestBed.resetTestingModule();
    sessionStorage.clear();
  });

  it('stores a successful login session', () => {
    const { authService, http } = configureTestBed();

    authService.login({ username: 'operator-user', password: 'password' }).subscribe();
    http.expectOne('/api/auth/login').flush(activeSession);

    expect(authService.accessToken).toBe(activeSession.accessToken);
    expect(authService.username).toBe(activeSession.username);
    expect(authService.scopes).toEqual(activeSession.scopes);
    expect(sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toContain(activeSession.accessToken);
    http.verify();
  });

  it('does not create a session after failed login', () => {
    const { authService, http } = configureTestBed();

    authService.login({ username: 'operator-user', password: 'wrong-password' }).subscribe({ error: () => undefined });
    http.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(authService.accessToken).toBeNull();
    expect(sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull();
    http.verify();
  });

  it('clears the session and navigates to login on logout', () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const { authService, router, http } = configureTestBed();

    authService.logout();

    expect(authService.accessToken).toBeNull();
    expect(sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
    http.verify();
  });

  it('rejects and clears an expired stored session', () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify({
      ...activeSession,
      expiresAtUtc: '2000-01-01T00:00:00.000Z'
    }));
    const { authService, http } = configureTestBed();

    expect(authService.hasValidSession()).toBe(false);
    expect(authService.accessToken).toBeNull();
    expect(sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull();
    http.verify();
  });

  it('redirects unauthenticated users from the chat route', () => {
    const { router, http } = configureTestBed();
    const redirect = {} as UrlTree;
    router.createUrlTree.mockReturnValue(redirect);

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBe(redirect);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    http.verify();
  });

  it('allows authenticated users to access the chat route', () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const { http } = configureTestBed();

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBe(true);
    http.verify();
  });

  it('attaches the bearer token to authenticated API requests', () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const { client, http } = configureTestBed();

    client.post('/api/chat', { message: 'Hello' }).subscribe();
    const request = http.expectOne('/api/chat');

    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${activeSession.accessToken}`);
    request.flush({ message: 'Hello', toolsUsed: [], ragSources: [] });
    http.verify();
  });

  it('does not attach the bearer token to the login endpoint', () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const { client, http } = configureTestBed();

    client.post('/api/auth/login', { username: 'operator-user', password: 'password' }).subscribe();
    const request = http.expectOne('/api/auth/login');

    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(activeSession);
    http.verify();
  });

  it('clears authentication and redirects to login after an authenticated 401', () => {
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(activeSession));
    const { authService, client, http, router } = configureTestBed();

    client.get('/api/chat').subscribe({ error: () => undefined });
    http.expectOne('/api/chat').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(authService.accessToken).toBeNull();
    expect(sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
    http.verify();
  });
});

function configureTestBed() {
  const router = {
    createUrlTree: vi.fn(),
    navigateByUrl: vi.fn().mockResolvedValue(true)
  };

  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: Router, useValue: router },
      { provide: CHAT_API_CONFIG, useValue: { baseUrl: '' } }
    ]
  });

  return {
    authService: TestBed.inject(AuthService),
    client: TestBed.inject(HttpClient),
    http: TestBed.inject(HttpTestingController),
    router
  };
}
