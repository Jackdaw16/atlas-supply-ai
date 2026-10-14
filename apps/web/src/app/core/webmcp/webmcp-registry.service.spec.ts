import { DestroyRef } from '@angular/core';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WebMcpModelContext, WebMcpTool } from './webmcp.types';
import { WebMcpRegistryService } from './webmcp-registry.service';

describe('WebMcpRegistryService', () => {
  afterEach(() => vi.restoreAllMocks());

  it('silently does nothing when the browser does not expose WebMCP', async () => {
    const original = document.modelContext;
    const debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
    document.modelContext = undefined;
    const registry = new WebMcpRegistryService();

    const registration = registry.register('suppliers', [tool('search_suppliers')]);

    await expect(registration.ready).resolves.toBeUndefined();
    expect(registry.diagnostics()).toEqual({
      webMcpApiAvailable: false,
      modelContextAvailable: false,
      registeredToolNames: [],
      registrationErrors: []
    });
    registration.dispose();
    expect(debug).not.toHaveBeenCalled();
    document.modelContext = original;
  });

  it('distinguishes a present modelContext from a usable WebMCP API', () => {
    const original = document.modelContext;
    document.modelContext = {} as WebMcpModelContext;

    const registry = new WebMcpRegistryService();

    expect(registry.diagnostics()).toMatchObject({
      webMcpApiAvailable: false,
      modelContextAvailable: true
    });
    document.modelContext = original;
  });

  it('logs support once, successful registrations, and destroy-driven unregistrations', async () => {
    const original = document.modelContext;
    const debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
    const registerTool = modelContextSpy();
    document.modelContext = { registerTool };
    const registry = new WebMcpRegistryService();
    let destroy: (() => void) | undefined;
    const destroyRef = {
      destroyed: false,
      onDestroy: (callback: () => void) => {
        destroy = callback;
        return () => undefined;
      }
    } as unknown as DestroyRef;

    const registration = registry.register('authenticated-global-reads', [tool('search_orders')]);
    await registration.ready;
    const secondRegistration = registry.register('suppliers-contextual', [tool('open_supplier')], destroyRef);
    await secondRegistration.ready;

    expect(registerTool).toHaveBeenCalledWith(expect.objectContaining({ name: 'search_orders' }), expect.objectContaining({ signal: expect.any(AbortSignal) }));
    expect(debug).toHaveBeenCalledWith('[WebMCP] supported');
    expect(debug).toHaveBeenCalledWith('[WebMCP] registered: search_orders');
    expect(debug.mock.calls.filter(([message]) => message === '[WebMCP] supported')).toHaveLength(1);
    expect(registry.diagnostics().registeredToolNames).toEqual(['open_supplier', 'search_orders']);
    expect(registry.diagnostics().registrationErrors).toEqual([]);
    const options = registrationOptions(registerTool, 0);
    destroy?.();

    expect(options.signal.aborted).toBe(false);
    expect(debug).toHaveBeenCalledWith('[WebMCP] unregistered: open_supplier');
    expect(registry.diagnostics().registeredToolNames).toEqual(['search_orders']);
    registration.dispose();
    document.modelContext = original;
  });

  it('does not log failed or aborted registrations as registered', async () => {
    const original = document.modelContext;
    const debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    let resolveRegistration: (() => void) | undefined;
    const registerTool = vi.fn<(tool: WebMcpTool, options?: { signal?: AbortSignal }) => Promise<void>>(
      () => new Promise<void>((resolve) => { resolveRegistration = resolve; })
    );
    document.modelContext = { registerTool };
    const registry = new WebMcpRegistryService();

    const aborted = registry.register('orders', [tool('search_orders')]);
    aborted.dispose();
    await Promise.resolve();
    resolveRegistration?.();
    await aborted.ready;

    document.modelContext = { registerTool: vi.fn(() => Promise.reject(new Error('registration failed'))) };
    const failed = registry.register('incidents', [tool('search_incidents')]);
    await failed.ready;

    expect(debug).not.toHaveBeenCalledWith('[WebMCP] registered: search_orders');
    expect(debug).not.toHaveBeenCalledWith('[WebMCP] registered: search_incidents');
    expect(warn).toHaveBeenCalledWith('[WebMCP] Registration failed for search_incidents.');
    expect(registry.diagnostics().registrationErrors).toEqual([
      { toolName: 'search_incidents', message: 'registration failed' }
    ]);
    failed.dispose();
    expect(registry.diagnostics().registrationErrors).toEqual([]);
    document.modelContext = original;
  });

  it('replaces a duplicate scoped registration without allowing stale cleanup to remove the replacement', async () => {
    const original = document.modelContext;
    const registerTool = modelContextSpy();
    document.modelContext = { registerTool };
    const registry = new WebMcpRegistryService();

    const first = registry.register('orders', [tool('search_orders')]);
    await first.ready;
    const firstSignal = registrationOptions(registerTool, 0).signal;
    const replacement = registry.register('orders', [tool('search_orders')]);
    await replacement.ready;
    const replacementSignal = registrationOptions(registerTool, 1).signal;

    first.dispose();

    expect(firstSignal.aborted).toBe(true);
    expect(replacementSignal.aborted).toBe(false);
    expect(registerTool).toHaveBeenCalledTimes(2);
    expect(registry.diagnostics().registeredToolNames).toEqual(['search_orders']);
    replacement.dispose();
    expect(registry.diagnostics().registeredToolNames).toEqual([]);
    document.modelContext = original;
  });
});

function tool(name: string): WebMcpTool {
  return {
    name,
    description: 'Test tool.',
    inputSchema: { type: 'object' },
    annotations: { readOnlyHint: true },
    execute: async () => ({ ok: true })
  };
}

function modelContextSpy() {
  return vi.fn<(tool: WebMcpTool, options?: { signal?: AbortSignal }) => Promise<void>>(() => Promise.resolve());
}

function registrationOptions(registerTool: ReturnType<typeof modelContextSpy>, index: number): { signal: AbortSignal } {
  const options = registerTool.mock.calls[index]?.[1];
  if (!options?.signal) {
    throw new Error('Expected registration options with an AbortSignal.');
  }
  return { signal: options.signal };
}
