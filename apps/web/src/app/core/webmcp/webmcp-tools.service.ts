import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, inject, Injectable, Injector, isDevMode } from '@angular/core';
import { firstValueFrom, fromEvent, Observable, takeUntil } from 'rxjs';
import { Incident, IncidentStatus, IncidentType } from '../../features/incidents/models/incident.models';
import { IncidentService } from '../../features/incidents/services/incident.service';
import { PurchaseOrder, PurchaseOrderStatus } from '../../features/orders/models/purchase-order.models';
import { PurchaseOrderService } from '../../features/orders/services/purchase-order.service';
import { Supplier } from '../../features/suppliers/models/supplier.models';
import { SupplierService } from '../../features/suppliers/services/supplier.service';
import { AuthService } from '../auth/auth.service';
import { WebMcpExecutionOptions, WebMcpTool } from './webmcp.types';
import { WebMcpRegistration, WebMcpRegistryService } from './webmcp-registry.service';

type ToolErrorCode = 'invalid_input' | 'not_found' | 'api_error' | 'api_unavailable' | 'navigation_unavailable' | 'unauthenticated' | 'unavailable' | 'unexpected_error';

type ToolFailure = {
  ok: false;
  valid?: false;
  error: {
    code: ToolErrorCode;
    message: string;
  };
};

type ToolResult = ({ ok: true } & Record<string, unknown>) | ToolFailure;
type InputResult<T> = { valid: true; value: T } | ToolFailure;

type SelectRecord<T> = (record: T) => boolean;

const PAGE_SIZE = 25;
const ORDER_STATUSES: readonly PurchaseOrderStatus[] = ['Draft', 'Submitted', 'Approved', 'Received', 'Cancelled'];
const INCIDENT_STATUSES: readonly IncidentStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed', 'Cancelled'];
const INCIDENT_TYPES: readonly IncidentType[] = ['Delay', 'QualityIssue', 'ShortShipment', 'DamagedGoods', 'Other'];

@Injectable({ providedIn: 'root' })
export class WebMcpToolsService {
  private readonly registry = inject(WebMcpRegistryService);
  private readonly injector = inject(Injector);

  registerGlobalReadTools(): WebMcpRegistration {
    return this.registry.register('authenticated-global-reads', [
      ...this.supplierReadTools(),
      ...this.orderReadTools(),
      ...this.incidentReadTools()
    ]);
  }

  registerSupplierTools(destroyRef: DestroyRef, selectSupplier: SelectRecord<Supplier>): WebMcpRegistration {
    return this.registry.register('suppliers-contextual', [
      this.openSupplierTool(selectSupplier)
    ], destroyRef);
  }

  private supplierReadTools(): WebMcpTool[] {
    return [
      this.tool('search_suppliers', 'Search the current supplier catalog by name or contact email.', {
        type: 'object', properties: { query: { type: 'string' } }, additionalProperties: false
      }, async (input, options) => {
        const query = this.optionalString(input, 'query');
        return query.valid ? this.execute(
          () => this.supplierService.page(0, PAGE_SIZE, query.value),
          options.signal,
          (page) => ({ items: page.items.map((supplier) => this.supplierRecord(supplier)), totalCount: page.totalCount })
        ) : query;
      }),
      this.tool('get_supplier', 'Get one supplier by its stable supplier ID.', this.idSchema('supplierId'), async (input, options) => {
        const supplierId = this.requiredId(input, 'supplierId');
        return supplierId.valid ? this.execute(
          () => this.supplierService.get(supplierId.value),
          options.signal,
          (supplier) => ({ supplier: this.supplierRecord(supplier) })
        ) : supplierId;
      })
    ];
  }

  registerOrderTools(destroyRef: DestroyRef, selectOrder: SelectRecord<PurchaseOrder>): WebMcpRegistration {
    return this.registry.register('orders-contextual', [this.openOrderTool(selectOrder)], destroyRef);
  }

  private orderReadTools(): WebMcpTool[] {
    return [
      this.tool('search_orders', 'Search purchase orders with the current Orders page filters.', {
        type: 'object',
        properties: {
          query: { type: 'string' }, supplierId: { type: 'string' }, status: { type: 'string', enum: ORDER_STATUSES }, delayed: { type: 'boolean' }
        },
        additionalProperties: false
      }, async (input, options) => {
        const query = this.optionalString(input, 'query');
        const supplierId = this.optionalId(input, 'supplierId');
        const status = this.optionalEnum(input, 'status', ORDER_STATUSES);
        const delayed = this.optionalBoolean(input, 'delayed');
        const invalid = this.firstInvalid(query, supplierId, status, delayed);
        if (invalid) {
          return invalid;
        }
        return this.execute(
          () => this.purchaseOrderService.page(0, PAGE_SIZE, {
            search: this.inputValue(query), supplierId: this.inputValue(supplierId), status: this.inputValue(status), isDelayed: this.inputValue(delayed)
          }),
          options.signal,
          (page) => ({ items: page.items.map((order) => this.orderRecord(order)), totalCount: page.totalCount })
        );
      }),
      this.tool('get_order', 'Get one purchase order with its supplier identity, timestamps, total, and items.', this.idSchema('orderId'), async (input, options) => {
        const orderId = this.requiredId(input, 'orderId');
        return orderId.valid ? this.getOrder(orderId.value, options.signal) : orderId;
      }),
      this.tool('get_delayed_orders', 'Get the first page of orders already marked delayed by the existing Orders API filter.', {
        type: 'object', properties: {}, additionalProperties: false
      }, async (_input, options) => this.execute(
        () => this.purchaseOrderService.page(0, PAGE_SIZE, { search: '', isDelayed: true }),
        options.signal,
        (page) => ({ items: page.items.map((order) => this.orderRecord(order)), totalCount: page.totalCount })
      )),
    ];
  }

  registerIncidentTools(destroyRef: DestroyRef, selectIncident: SelectRecord<Incident>): WebMcpRegistration {
    return this.registry.register('incidents-contextual', [this.openIncidentTool(selectIncident)], destroyRef);
  }

  private incidentReadTools(): WebMcpTool[] {
    return [
      this.tool('search_incidents', 'Search incidents with the current Incidents page filters.', {
        type: 'object',
        properties: {
          query: { type: 'string' }, supplierId: { type: 'string' }, status: { type: 'string', enum: INCIDENT_STATUSES },
          type: { type: 'string', enum: INCIDENT_TYPES }, lifecycle: { type: 'string', enum: ['open', 'resolved'] }
        },
        additionalProperties: false
      }, async (input, options) => {
        const query = this.optionalString(input, 'query');
        const supplierId = this.optionalId(input, 'supplierId');
        const status = this.optionalEnum(input, 'status', INCIDENT_STATUSES);
        const type = this.optionalEnum(input, 'type', INCIDENT_TYPES);
        const lifecycle = this.optionalEnum(input, 'lifecycle', ['open', 'resolved'] as const);
        const invalid = this.firstInvalid(query, supplierId, status, type, lifecycle);
        if (invalid) {
          return invalid;
        }
        return this.execute(
          () => this.incidentService.page(0, PAGE_SIZE, {
            search: this.inputValue(query), supplierId: this.inputValue(supplierId), status: this.inputValue(status),
            type: this.inputValue(type), lifecycle: this.inputValue(lifecycle)
          }),
          options.signal,
          (page) => ({ items: page.items.map((incident) => this.incidentRecord(incident)), totalCount: page.totalCount })
        );
      }),
      this.tool('get_incident', 'Get one incident by its stable incident ID.', this.idSchema('incidentId'), async (input, options) => {
        const incidentId = this.requiredId(input, 'incidentId');
        return incidentId.valid ? this.execute(
          () => this.incidentService.get(incidentId.value),
          options.signal,
          (incident) => ({ incident: this.incidentRecord(incident) })
        ) : incidentId;
      }),
    ];
  }

  private openSupplierTool(selectSupplier: SelectRecord<Supplier>): WebMcpTool {
    return this.tool('open_supplier', 'Open one supplier in the visible Suppliers page detail state.', this.idSchema('supplierId'), async (input, options) => {
      const supplierId = this.requiredId(input, 'supplierId');
      if (!supplierId.valid) {
        return supplierId;
      }
      return this.execute(() => this.supplierService.get(supplierId.value), options.signal, (supplier) => {
        if (!selectSupplier(supplier)) {
          return this.failure('navigation_unavailable', 'The Suppliers page is no longer available to open this supplier.');
        }
        return { supplier: this.supplierRecord(supplier), selected: true };
      });
    });
  }

  private openOrderTool(selectOrder: SelectRecord<PurchaseOrder>): WebMcpTool {
    return this.tool('open_order', 'Open one purchase order in the visible Orders page detail state.', this.idSchema('orderId'), async (input, options) => {
      const orderId = this.requiredId(input, 'orderId');
      if (!orderId.valid) {
        return orderId;
      }
      return this.execute(() => this.purchaseOrderService.get(orderId.value), options.signal, (order) => {
        if (!selectOrder(order)) {
          return this.failure('navigation_unavailable', 'The Orders page is no longer available to open this order.');
        }
        return { order: this.orderRecord(order), selected: true };
      });
    });
  }

  private openIncidentTool(selectIncident: SelectRecord<Incident>): WebMcpTool {
    return this.tool('open_incident', 'Open one incident in the visible Incidents page detail state.', this.idSchema('incidentId'), async (input, options) => {
      const incidentId = this.requiredId(input, 'incidentId');
      if (!incidentId.valid) {
        return incidentId;
      }
      return this.execute(() => this.incidentService.get(incidentId.value), options.signal, (incident) => {
        if (!selectIncident(incident)) {
          return this.failure('navigation_unavailable', 'The Incidents page is no longer available to open this incident.');
        }
        return { incident: this.incidentRecord(incident), selected: true };
      });
    });
  }

  private tool(
    name: string,
    description: string,
    inputSchema: Record<string, unknown>,
    execute: WebMcpTool['execute']
  ): WebMcpTool {
    return {
      name,
      description,
      inputSchema,
      annotations: { readOnlyHint: true },
      execute: async (input, options) => {
        if (!this.authService.hasValidSession()) {
          this.authService.handleUnauthorized();
          return this.failure('unauthenticated', 'An active authenticated session is required to read Atlas Supply data.');
        }

        return execute(input, options);
      }
    };
  }

  private idSchema(name: string): Record<string, unknown> {
    return {
      type: 'object', properties: { [name]: { type: 'string', minLength: 1 } }, required: [name], additionalProperties: false
    };
  }

  private get supplierService(): SupplierService {
    return this.injector.get(SupplierService);
  }

  private get authService(): AuthService {
    return this.injector.get(AuthService);
  }

  private get purchaseOrderService(): PurchaseOrderService {
    return this.injector.get(PurchaseOrderService);
  }

  private get incidentService(): IncidentService {
    return this.injector.get(IncidentService);
  }

  private async getOrder(orderId: string, signal: AbortSignal): Promise<ToolResult> {
    try {
      const order = await this.request(() => this.purchaseOrderService.get(orderId), signal);
      const supplierName = await this.supplierName(order.supplierId, signal);
      return { ok: true, order: this.orderRecord(order, supplierName) };
    } catch (error) {
      return this.errorResult(error, signal);
    }
  }

  private async supplierName(supplierId: string, signal: AbortSignal): Promise<string | undefined> {
    try {
      return (await this.request(() => this.supplierService.get(supplierId), signal)).name;
    } catch {
      if (isDevMode() && !signal.aborted) {
        console.warn('[WebMCP] Supplier name was unavailable while reading an order.');
      }
      return undefined;
    }
  }

  private async execute<T>(
    request: () => Observable<T>,
    signal: AbortSignal,
    map: (value: T) => Record<string, unknown> | ToolFailure
  ): Promise<ToolResult> {
    try {
      const mapped = map(await this.request(request, signal));
      return this.isToolFailure(mapped) ? mapped : { ok: true, ...mapped };
    } catch (error) {
      return this.errorResult(error, signal);
    }
  }

  private request<T>(request: () => Observable<T>, signal: AbortSignal): Promise<T> {
    if (signal.aborted) {
      return Promise.reject(new Error('WebMCP tool execution was aborted.'));
    }
    return firstValueFrom(request().pipe(takeUntil(fromEvent(signal, 'abort'))));
  }

  private requiredId(input: Record<string, unknown>, key: string): InputResult<string> {
    const value = input[key];
    return typeof value === 'string' && value.trim()
      ? { valid: true, value: value.trim() }
      : this.failure('invalid_input', `${key} must be a non-empty string.`);
  }

  private optionalId(input: Record<string, unknown>, key: string): InputResult<string | undefined> {
    if (input[key] === undefined) {
      return { valid: true, value: undefined };
    }
    const value = this.requiredId(input, key);
    return value;
  }

  private optionalString(input: Record<string, unknown>, key: string): InputResult<string> {
    const value = input[key];
    return value === undefined || typeof value === 'string'
      ? { valid: true, value: typeof value === 'string' ? value.trim() : '' }
      : this.failure('invalid_input', `${key} must be a string when provided.`);
  }

  private optionalBoolean(input: Record<string, unknown>, key: string): InputResult<boolean | undefined> {
    const value = input[key];
    return value === undefined || typeof value === 'boolean'
      ? { valid: true, value }
      : this.failure('invalid_input', `${key} must be a boolean when provided.`);
  }

  private optionalEnum<T extends string>(
    input: Record<string, unknown>,
    key: string,
    values: readonly T[]
  ): InputResult<T | undefined> {
    const value = input[key];
    return value === undefined || (typeof value === 'string' && values.includes(value as T))
      ? { valid: true, value: value as T | undefined }
      : this.failure('invalid_input', `${key} must be one of: ${values.join(', ')}.`);
  }

  private firstInvalid(...results: InputResult<unknown>[]): ToolFailure | undefined {
    return results.find((result): result is ToolFailure => !result.valid);
  }

  private inputValue<T>(result: InputResult<T>): T {
    if (!result.valid) {
      throw new Error('Invalid tool input was used after validation.');
    }
    return result.value;
  }

  private isToolFailure(value: Record<string, unknown> | ToolFailure): value is ToolFailure {
    return value.ok === false;
  }

  private errorResult(error: unknown, signal: AbortSignal): ToolResult {
    if (signal.aborted) {
      return this.failure('unavailable', 'The tool execution was cancelled because this page is no longer active.');
    }
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) {
        return this.failure('not_found', 'The requested resource was not found.');
      }
      if (error.status === 0) {
        return this.failure('api_unavailable', 'The Atlas Supply API could not be reached.');
      }
      return this.failure('api_error', 'The Atlas Supply API could not complete this read request.');
    }
    if (isDevMode()) {
      console.warn('[WebMCP] An unexpected tool execution error occurred.');
    }
    return this.failure('unexpected_error', 'The read request could not be completed.');
  }

  private failure(code: ToolErrorCode, message: string): ToolFailure {
    return { ok: false, error: { code, message } };
  }

  private supplierRecord(supplier: Supplier): Record<string, unknown> {
    return { id: supplier.id, name: supplier.name, contactEmail: supplier.contactEmail, isActive: supplier.isActive };
  }

  private orderRecord(order: PurchaseOrder, supplierName?: string): Record<string, unknown> {
    return {
      id: order.id,
      supplier: { id: order.supplierId, ...(supplierName ? { name: supplierName } : {}) },
      status: order.status,
      createdAtUtc: order.createdAtUtc,
      submittedAtUtc: order.submittedAtUtc,
      approvedAtUtc: order.approvedAtUtc,
      receivedAtUtc: order.receivedAtUtc,
      isDelayed: order.isDelayed,
      totalAmount: order.totalAmount,
      items: order.items.map((item) => ({ id: item.id, description: item.description, quantity: item.quantity, unitPrice: item.unitPrice, lineTotal: item.lineTotal }))
    };
  }

  private incidentRecord(incident: Incident): Record<string, unknown> {
    return {
      id: incident.id,
      type: incident.type,
      status: incident.status,
      supplierId: incident.supplierId,
      purchaseOrderId: incident.purchaseOrderId,
      description: incident.description,
      createdAtUtc: incident.createdAtUtc,
      resolvedAtUtc: incident.resolvedAtUtc,
      closedAtUtc: incident.closedAtUtc
    };
  }
}
