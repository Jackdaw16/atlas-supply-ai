import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { Subject } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../../core/auth/auth.service';
import { AuthSession } from '../../../../core/auth/auth.models';
import { CHAT_HISTORY_STORAGE_KEY_PREFIX } from '../../../../core/auth/auth-storage-keys';
import { ChatHistoryMessage, ChatMessage } from '../../models/chat-message.models';
import { ChatService, ChatStreamEvent } from '../../services/chat.service';
import { ChatComponent } from './chat.component';

const activeSession: AuthSession = {
  accessToken: 'test-access-token',
  expiresAtUtc: '2030-01-01T00:00:00.000Z',
  username: 'alice@example.test',
  scopes: ['agent.chat']
};

describe('ChatComponent history', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
    sessionStorage.clear();
  });

  it('restores finalized messages only for the authenticated user', async () => {
    writeHistory(activeSession.username, [
      { role: 'user', content: 'Previous question' },
      { role: 'assistant', content: 'Previous answer' }
    ]);
    writeHistory('other@example.test', [{ role: 'user', content: 'Other user history' }]);

    const { component } = await createComponent();

    expect(state(component).messages()).toMatchObject([
      { role: 'user', content: 'Previous question' },
      { role: 'assistant', content: 'Previous answer' }
    ]);
    expect(state(component).messages()).not.toContainEqual(expect.objectContaining({ content: 'Other user history' }));
  });

  it('clears the active user transcript, including the visible messages and retry state, when authentication disappears', async () => {
    writeHistory(activeSession.username, [{ role: 'user', content: 'Remove me' }]);
    writeHistory('other@example.test', [{ role: 'user', content: 'Keep me' }]);
    const { component, authSession } = await createComponent();
    state(component).retryMessageId = 1;

    authSession.set(null);
    await TestBed.tick();

    expect(sessionStorage.getItem(historyKey(activeSession.username))).toBeNull();
    expect(sessionStorage.getItem(historyKey('other@example.test'))).not.toBeNull();
    expect(state(component).messages()).toEqual([]);
    expect(state(component).retryMessageId).toBeNull();
  });

  it('removes invalid persisted values and keeps the newest history within both limits', async () => {
    const messages = Array.from({ length: 12 }, (_, index) => ({ role: 'user' as const, content: `Message ${index}` }));
    sessionStorage.setItem(historyKey(activeSession.username), JSON.stringify([{}, ...messages]));

    const { component } = await createComponent();

    expect(state(component).messages().map((message) => message.content)).toEqual(messages.slice(-10).map((message) => message.content));
    expect(readHistory(activeSession.username)).toEqual(messages.slice(-10));

    sessionStorage.setItem(historyKey(activeSession.username), JSON.stringify([{ role: 'assistant', content: 'x'.repeat(16 * 1024 + 1) }]));
    TestBed.resetTestingModule();
    const recreated = await createComponent();

    expect(state(recreated.component).messages()).toEqual([]);
    expect(sessionStorage.getItem(historyKey(activeSession.username))).toBeNull();
  });

  it('persists no pending data and sends prior finalized history without the current user message', async () => {
    writeHistory(activeSession.username, [
      { role: 'user', content: 'Prior question' },
      { role: 'assistant', content: 'Prior answer' }
    ]);
    const stream = new Subject<ChatStreamEvent>();
    const chatService = { send: vi.fn().mockReturnValue(stream), abortActiveStream: vi.fn() };
    const { component } = await createComponent(chatService);

    state(component).draft = 'Current question';
    state(component).send();

    expect(chatService.send).toHaveBeenCalledWith({
      message: 'Current question',
      history: [
        { role: 'user', content: 'Prior question' },
        { role: 'assistant', content: 'Prior answer' }
      ]
    });
    expect(readHistory(activeSession.username)).toEqual([
      { role: 'user', content: 'Prior question' },
      { role: 'assistant', content: 'Prior answer' },
      { role: 'user', content: 'Current question' }
    ]);
    stream.next({ type: 'status', status: 'tool' });
    expect(readHistory(activeSession.username)).toEqual([
      { role: 'user', content: 'Prior question' },
      { role: 'assistant', content: 'Prior answer' },
      { role: 'user', content: 'Current question' }
    ]);

    stream.next({
      type: 'completed',
      response: {
        message: 'Current answer',
        toolsUsed: ['search_orders'],
        ragSources: [{ sourcePath: 'private-source', heading: 'Hidden from history' }]
      }
    });

    expect(readHistory(activeSession.username)).toEqual([
      { role: 'user', content: 'Prior question' },
      { role: 'assistant', content: 'Prior answer' },
      { role: 'user', content: 'Current question' },
      { role: 'assistant', content: 'Current answer' }
    ]);
  });

  it('retries a failed user message without duplicating it in the view, storage, or outbound history', async () => {
    const firstStream = new Subject<ChatStreamEvent>();
    const secondStream = new Subject<ChatStreamEvent>();
    const chatService = {
      send: vi.fn().mockReturnValueOnce(firstStream).mockReturnValueOnce(secondStream),
      abortActiveStream: vi.fn()
    };
    const { component } = await createComponent(chatService);

    state(component).draft = 'Retry this';
    state(component).send();
    firstStream.error(new Error('Unavailable'));
    state(component).send();

    expect(state(component).messages().filter((message) => message.role === 'user')).toHaveLength(1);
    expect(chatService.send).toHaveBeenLastCalledWith({ message: 'Retry this', history: [] });
    expect(readHistory(activeSession.username)).toEqual([{ role: 'user', content: 'Retry this' }]);
  });

  it('confirms before clearing a non-empty conversation and clears an empty conversation without confirmation', async () => {
    writeHistory(activeSession.username, [{ role: 'user', content: 'Keep this unless confirmed' }]);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { component } = await createComponent();

    state(component).startNewConversation();

    expect(confirm).toHaveBeenCalledOnce();
    expect(state(component).messages()).toHaveLength(1);
    expect(readHistory(activeSession.username)).toEqual([{ role: 'user', content: 'Keep this unless confirmed' }]);

    confirm.mockReturnValue(true);
    state(component).startNewConversation();

    expect(state(component).messages()).toEqual([]);
    expect(sessionStorage.getItem(historyKey(activeSession.username))).toBeNull();

    confirm.mockClear();
    state(component).startNewConversation();

    expect(confirm).not.toHaveBeenCalled();
  });

  it('removes only the current user chat history when starting a new conversation', async () => {
    writeHistory(activeSession.username, [{ role: 'user', content: 'Remove this' }]);
    writeHistory('other@example.test', [{ role: 'user', content: 'Keep this' }]);
    sessionStorage.setItem('atlas-supply.auth-session', 'keep-authentication');
    sessionStorage.setItem('unrelated-session-value', 'keep-this');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { component } = await createComponent();

    state(component).startNewConversation();

    expect(sessionStorage.getItem(historyKey(activeSession.username))).toBeNull();
    expect(readHistory('other@example.test')).toEqual([{ role: 'user', content: 'Keep this' }]);
    expect(sessionStorage.getItem('atlas-supply.auth-session')).toBe('keep-authentication');
    expect(sessionStorage.getItem('unrelated-session-value')).toBe('keep-this');
  });

  it('aborts an active stream and ignores its late status, completion, and error events after reset', async () => {
    const stream = new Subject<ChatStreamEvent>();
    const chatService = { send: vi.fn().mockReturnValue(stream), abortActiveStream: vi.fn() };
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { component } = await createComponent(chatService);

    state(component).draft = 'Question in progress';
    state(component).send();
    state(component).startNewConversation();
    stream.next({ type: 'status', status: 'tool' });
    stream.next({
      type: 'completed',
      response: { message: 'Late answer', toolsUsed: [], ragSources: [] }
    });
    stream.error(new Error('Late failure'));

    expect(chatService.abortActiveStream).toHaveBeenCalledOnce();
    expect(state(component).messages()).toEqual([]);
    expect(state(component).draft).toBe('');
    expect(state(component).errorMessage()).toBeNull();
    expect(state(component).isPending()).toBe(false);
    expect(state(component).retryMessageId).toBeNull();
    expect(sessionStorage.getItem(historyKey(activeSession.username))).toBeNull();
  });

  it('sends empty history for the first request after reset', async () => {
    const firstStream = new Subject<ChatStreamEvent>();
    const secondStream = new Subject<ChatStreamEvent>();
    const chatService = {
      send: vi.fn().mockReturnValueOnce(firstStream).mockReturnValueOnce(secondStream),
      abortActiveStream: vi.fn()
    };
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { component } = await createComponent(chatService);

    state(component).draft = 'Clear this request';
    state(component).send();
    state(component).startNewConversation();
    state(component).draft = 'Fresh request';
    state(component).send();

    expect(chatService.send).toHaveBeenLastCalledWith({ message: 'Fresh request', history: [] });
  });
});

async function createComponent(chatService?: { send: ReturnType<typeof vi.fn>; abortActiveStream: ReturnType<typeof vi.fn> }) {
  const authSession = signal<AuthSession | null>(activeSession);
  const authService = {
    session: authSession.asReadonly(),
    get username() {
      return authSession()?.username ?? null;
    }
  };
  const service = chatService ?? { send: vi.fn(), abortActiveStream: vi.fn() };

  TestBed.configureTestingModule({
    imports: [ChatComponent],
    providers: [
      { provide: AuthService, useValue: authService },
      { provide: ChatService, useValue: service }
    ]
  });
  const fixture = TestBed.createComponent(ChatComponent);
  fixture.detectChanges();
  await fixture.whenStable();

  return { component: fixture.componentInstance, fixture, authSession, chatService: service };
}

function state(component: ChatComponent): {
  draft: string;
  errorMessage: WritableSignal<string | null>;
  isPending: WritableSignal<boolean>;
  messages: WritableSignal<ChatMessage[]>;
  retryMessageId: number | null;
  send(): void;
  startNewConversation(): void;
} {
  return component as unknown as {
    draft: string;
    errorMessage: WritableSignal<string | null>;
    isPending: WritableSignal<boolean>;
    messages: WritableSignal<ChatMessage[]>;
    retryMessageId: number | null;
    send(): void;
    startNewConversation(): void;
  };
}

function historyKey(username: string): string {
  return `${CHAT_HISTORY_STORAGE_KEY_PREFIX}${encodeURIComponent(username)}`;
}

function writeHistory(username: string, history: ChatHistoryMessage[]): void {
  sessionStorage.setItem(historyKey(username), JSON.stringify(history));
}

function readHistory(username: string): ChatHistoryMessage[] {
  return JSON.parse(sessionStorage.getItem(historyKey(username)) ?? '[]') as ChatHistoryMessage[];
}
