import { DestroyRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { IncidentService } from '../../features/incidents/services/incident.service';
import { PurchaseOrderService } from '../../features/orders/services/purchase-order.service';
import { SupplierService } from '../../features/suppliers/services/supplier.service';
import { WebMcpTool } from './webmcp.types';
import { WebMcpRegistryService } from './webmcp-registry.service';
import { WebMcpToolsService } from './webmcp-tools.service';

describe('WebMcpToolsService', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('registers the seven global reads with their existing names and schemas', async () => {
    const page = vi.fn(() => of({ items: [order()], totalCount: 1 }));
    const registry = registrationSpy();
    configure(registry, { page, get: vi.fn(() => of(order())) });
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    const tools = registeredTools(registry, 0);
    const search = findTool(tools, 'search_orders');
    const result = await search.execute({ query: '  bolts ', status: 'Approved', delayed: true }, executionOptions());

    expect(tools.map((tool) => tool.name)).toEqual([
      'search_suppliers', 'get_supplier', 'search_orders', 'get_order', 'get_delayed_orders', 'search_incidents', 'get_incident'
    ]);
    expect(search.inputSchema).toEqual({
      type: 'object',
      properties: {
        query: { type: 'string' }, supplierId: { type: 'string' }, status: { type: 'string', enum: ['Draft', 'Submitted', 'Approved', 'Received', 'Cancelled'] }, delayed: { type: 'boolean' }
      },
      additionalProperties: false
    });
    expect(page).toHaveBeenCalledWith(0, 25, {
      search: 'bolts', status: 'Approved', supplierId: undefined, isDelayed: true
    });
    expect(result).toEqual(expect.objectContaining({ ok: true, totalCount: 1, items: [expect.objectContaining({ id: 'order-1', isDelayed: true })] }));
  });

  it('registers only one contextual open tool for each page without overlapping the global names', () => {
    const registry = registrationSpy();
    configure(registry, { page: vi.fn(), get: vi.fn(() => of(order())) });
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    service.registerSupplierTools(destroyRef(), () => true);
    service.registerOrderTools(destroyRef(), () => true);
    service.registerIncidentTools(destroyRef(), () => true);

    expect((registry.register.mock.calls as unknown as [string, WebMcpTool[]][]).map(([scope]) => scope)).toEqual([
      'authenticated-global-reads', 'suppliers-contextual', 'orders-contextual', 'incidents-contextual'
    ]);
    expect(registeredTools(registry, 1).map((tool) => tool.name)).toEqual(['open_supplier']);
    expect(registeredTools(registry, 2).map((tool) => tool.name)).toEqual(['open_order']);
    expect(registeredTools(registry, 3).map((tool) => tool.name)).toEqual(['open_incident']);
    expect(registeredTools(registry, 0).map((tool) => tool.name)).not.toContain('open_supplier');
  });

  it('prevents a stale global tool from reading after the authenticated session is cleared', async () => {
    const get = vi.fn(() => of(order()));
    const registry = registrationSpy();
    const handleUnauthorized = vi.fn();
    configure(registry, { page: vi.fn(), get }, false, handleUnauthorized);
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    const result = await findTool(registeredTools(registry, 0), 'get_order').execute({ orderId: 'order-1' }, executionOptions());
    service.registerSupplierTools(destroyRef(), () => true);
    const contextualResult = await findTool(registeredTools(registry, 1), 'open_supplier').execute({ supplierId: 'supplier-1' }, executionOptions());

    expect(get).not.toHaveBeenCalled();
    expect(handleUnauthorized).toHaveBeenCalledTimes(2);
    expect(result).toEqual({
      ok: false,
      error: { code: 'unauthenticated', message: 'An active authenticated session is required to read Atlas Supply data.' }
    });
    expect(contextualResult).toEqual(result);
  });

  it('returns a structured error instead of calling services for an invalid resource ID', async () => {
    const registry = registrationSpy();
    const get = vi.fn();
    configure(registry, { page: vi.fn(), get });
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    const result = await findTool(registeredTools(registry, 0), 'get_incident').execute({ incidentId: '   ' }, executionOptions());

    expect(get).not.toHaveBeenCalled();
    expect(result).toEqual({ ok: false, error: { code: 'invalid_input', message: 'incidentId must be a non-empty string.' } });
  });
});

function configure(
  registry: { register: ReturnType<typeof vi.fn> },
  orderService: { page: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn> },
  hasValidSession = true,
  handleUnauthorized = vi.fn()
): void {
  TestBed.configureTestingModule({
    providers: [
      { provide: WebMcpRegistryService, useValue: registry },
      { provide: AuthService, useValue: { hasValidSession: () => hasValidSession, handleUnauthorized } },
      { provide: SupplierService, useValue: { page: vi.fn(), get: orderService.get } },
      { provide: PurchaseOrderService, useValue: orderService },
      { provide: IncidentService, useValue: { page: vi.fn(), get: orderService.get } }
    ]
  });
}

function registrationSpy() {
  return { register: vi.fn(() => ({ ready: Promise.resolve(), dispose: vi.fn() })) };
}

function registeredTools(registry: { register: ReturnType<typeof vi.fn> }, callIndex: number): WebMcpTool[] {
  return (registry.register.mock.calls as unknown as [string, WebMcpTool[]][])[callIndex][1];
}

function findTool(tools: readonly WebMcpTool[], name: string): WebMcpTool {
  const tool = tools.find((candidate) => candidate.name === name);
  if (!tool) {
    throw new Error(`Missing ${name}.`);
  }
  return tool;
}

function executionOptions() {
  return { signal: new AbortController().signal };
}

function destroyRef(): DestroyRef {
  return { destroyed: false, onDestroy: () => undefined } as unknown as DestroyRef;
}

function order() {
  return {
    id: 'order-1', supplierId: 'supplier-1', status: 'Approved' as const, createdAtUtc: '2026-01-01T00:00:00Z',
    submittedAtUtc: '2026-01-02T00:00:00Z', approvedAtUtc: '2026-01-03T00:00:00Z', receivedAtUtc: null,
    items: [{ id: 'item-1', description: 'Bolts', quantity: 4, unitPrice: 2.5, lineTotal: 10 }], totalAmount: 10, isDelayed: true
  };
}
