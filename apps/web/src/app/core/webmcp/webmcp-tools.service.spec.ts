import { DestroyRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
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

  it('registers the eight global tools with their existing read schemas and navigation schema', async () => {
    const page = vi.fn(() => of({ items: [order()], totalCount: 1 }));
    const registry = registrationSpy();
    configure(registry, { page, get: vi.fn(() => of(order())) });
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    const tools = registeredTools(registry, 0);
    const search = findTool(tools, 'search_orders');
    const navigate = findTool(tools, 'navigate_to_section');
    const result = await search.execute({ query: '  bolts ', status: 'Approved', delayed: true }, executionOptions());

    expect(tools.map((tool) => tool.name)).toEqual([
      'search_suppliers', 'get_supplier', 'search_orders', 'get_order', 'get_delayed_orders', 'search_incidents', 'get_incident', 'navigate_to_section'
    ]);
    expect(search.inputSchema).toEqual({
      type: 'object',
      properties: {
        query: { type: 'string' }, supplierId: { type: 'string' }, status: { type: 'string', enum: ['Draft', 'Submitted', 'Approved', 'Received', 'Cancelled'] }, delayed: { type: 'boolean' }
      },
      additionalProperties: false
    });
    expect(navigate.inputSchema).toEqual({
      type: 'object',
      properties: { section: { type: 'string', enum: ['chat', 'suppliers', 'orders', 'incidents'] } },
      required: ['section'],
      additionalProperties: false
    });
    expect(page).toHaveBeenCalledWith(0, 25, {
      search: 'bolts', status: 'Approved', supplierId: undefined, isDelayed: true
    });
    expect(result).toEqual(expect.objectContaining({ ok: true, totalCount: 1, items: [expect.objectContaining({ id: 'order-1', isDelayed: true })] }));
  });

  it('navigates to the requested authenticated section and reports the reached route', async () => {
    const registry = registrationSpy();
    const router = navigationSpy();
    configure(registry, { page: vi.fn(), get: vi.fn(() => of(order())) }, true, vi.fn(), router);
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    const result = await findTool(registeredTools(registry, 0), 'navigate_to_section').execute({ section: 'orders' }, executionOptions());

    expect(router.navigateByUrl).toHaveBeenCalledWith('/orders');
    expect(result).toEqual({
      ok: true,
      requestedSection: 'orders',
      reachedSection: 'orders',
      reachedRoute: '/orders'
    });
  });

  it('returns a navigation error when the router rejects, returns false, or reaches another route', async () => {
    const registry = registrationSpy();
    const router = navigationSpy();
    router.navigateByUrl.mockResolvedValueOnce(false).mockRejectedValueOnce(new Error('Navigation failed')).mockResolvedValueOnce(true);
    configure(registry, { page: vi.fn(), get: vi.fn(() => of(order())) }, true, vi.fn(), router);
    const service = TestBed.inject(WebMcpToolsService);

    service.registerGlobalReadTools();
    const navigate = findTool(registeredTools(registry, 0), 'navigate_to_section');
    const falseResult = await navigate.execute({ section: 'suppliers' }, executionOptions());
    const rejectedResult = await navigate.execute({ section: 'incidents' }, executionOptions());
    const wrongRouteResult = await navigate.execute({ section: 'orders' }, executionOptions());

    const expectedError = {
      ok: false,
      error: { code: 'navigation_unavailable', message: 'The requested section could not be reached.' }
    };
    expect(falseResult).toEqual(expectedError);
    expect(rejectedResult).toEqual(expectedError);
    expect(wrongRouteResult).toEqual(expectedError);
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
  handleUnauthorized = vi.fn(),
  router = navigationSpy()
): void {
  TestBed.configureTestingModule({
    providers: [
      { provide: WebMcpRegistryService, useValue: registry },
      { provide: AuthService, useValue: { hasValidSession: () => hasValidSession, handleUnauthorized } },
      { provide: Router, useValue: router },
      { provide: SupplierService, useValue: { page: vi.fn(), get: orderService.get } },
      { provide: PurchaseOrderService, useValue: orderService },
      { provide: IncidentService, useValue: { page: vi.fn(), get: orderService.get } }
    ]
  });
}

function registrationSpy() {
  return { register: vi.fn(() => ({ ready: Promise.resolve(), dispose: vi.fn() })) };
}

function navigationSpy() {
  const router = { url: '/chat', navigateByUrl: vi.fn() };
  router.navigateByUrl.mockImplementation(async (url: string) => {
    router.url = url;
    return true;
  });
  return router;
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
