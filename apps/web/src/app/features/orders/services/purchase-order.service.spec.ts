import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { PurchaseOrderService } from './purchase-order.service';

describe('PurchaseOrderService', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('uses the configured API base URL for order operations', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: CHAT_API_CONFIG, useValue: { baseUrl: 'https://api.atlas.test' } }]
    });
    const service = TestBed.inject(PurchaseOrderService);
    const http = TestBed.inject(HttpTestingController);
    const order = { id: 'order-1', supplierId: 'supplier-1', status: 'Draft' as const, createdAtUtc: '2026-01-01T00:00:00Z', submittedAtUtc: null, approvedAtUtc: null, receivedAtUtc: null, items: [], totalAmount: 0, isDelayed: false };

    service.list().subscribe();
    const listRequest = http.expectOne('https://api.atlas.test/api/orders');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([order]);

    service.updateItems(order.id, { items: [] }).subscribe();
    const updateRequest = http.expectOne('https://api.atlas.test/api/orders/order-1/items');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(order);

    service.submit(order.id).subscribe();
    const submitRequest = http.expectOne('https://api.atlas.test/api/orders/order-1/submit');
    expect(submitRequest.request.method).toBe('POST');
    submitRequest.flush(order);
    http.verify();
  });
});
