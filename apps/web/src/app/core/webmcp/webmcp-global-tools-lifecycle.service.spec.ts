import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CHAT_API_CONFIG } from '../services/chat-api.config';
import { AuthService, AUTH_SESSION_STORAGE_KEY } from '../auth/auth.service';
import { WebMcpToolsService } from './webmcp-tools.service';
import { WebMcpGlobalToolsLifecycleService } from './webmcp-global-tools-lifecycle.service';

describe('WebMcpGlobalToolsLifecycleService', () => {
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
    sessionStorage.clear();
  });

  it('registers once for an authenticated root session, is independent of route components, and disposes on logout', () => {
    const session = signal<unknown>({ accessToken: 'token' });
    const dispose = vi.fn();
    const registerGlobalReadTools = vi.fn(() => ({ ready: Promise.resolve(), dispose }));
    configure(session, registerGlobalReadTools);
    const lifecycle = TestBed.inject(WebMcpGlobalToolsLifecycleService);

    lifecycle.start();
    lifecycle.start();
    TestBed.flushEffects();

    expect(registerGlobalReadTools).toHaveBeenCalledTimes(1);

    session.set(null);
    TestBed.flushEffects();

    expect(dispose).toHaveBeenCalledTimes(1);
  });

  it('does not register global reads for an unauthenticated root session', () => {
    const session = signal<unknown>(null);
    const registerGlobalReadTools = vi.fn();
    configure(session, registerGlobalReadTools);

    TestBed.inject(WebMcpGlobalToolsLifecycleService).start();
    TestBed.flushEffects();

    expect(registerGlobalReadTools).not.toHaveBeenCalled();
  });

  it('disposes global tools when AuthService passively expires its restored session', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2029-01-01T00:00:00.000Z'));
    sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify({
      accessToken: 'test-access-token',
      expiresAtUtc: '2029-01-01T00:00:01.000Z',
      username: 'operator-user',
      scopes: []
    }));
    const dispose = vi.fn();
    const registerGlobalReadTools = vi.fn(() => ({ ready: Promise.resolve(), dispose }));
    configureWithAuthService(registerGlobalReadTools);
    const lifecycle = TestBed.inject(WebMcpGlobalToolsLifecycleService);
    const authService = TestBed.inject(AuthService);

    lifecycle.start();
    TestBed.flushEffects();
    vi.advanceTimersByTime(1_000);
    TestBed.flushEffects();

    expect(authService.session()).toBeNull();
    expect(dispose).toHaveBeenCalledTimes(1);
  });
});

function configure(session: ReturnType<typeof signal<unknown>>, registerGlobalReadTools: ReturnType<typeof vi.fn>): void {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: AuthService,
        useValue: {
          session: session.asReadonly(),
          hasValidSession: () => session() !== null
        }
      },
      { provide: WebMcpToolsService, useValue: { registerGlobalReadTools } }
    ]
  });
}

function configureWithAuthService(registerGlobalReadTools: ReturnType<typeof vi.fn>): void {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      { provide: Router, useValue: { navigateByUrl: vi.fn().mockResolvedValue(true) } },
      { provide: CHAT_API_CONFIG, useValue: { baseUrl: '' } },
      { provide: WebMcpToolsService, useValue: { registerGlobalReadTools } }
    ]
  });
}
