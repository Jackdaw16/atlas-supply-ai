import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { SupplierService } from './supplier.service';

describe('SupplierService', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('uses the configured API base URL for supplier operations', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CHAT_API_CONFIG, useValue: { baseUrl: 'https://api.atlas.test' } }
      ]
    });
    const service = TestBed.inject(SupplierService);
    const http = TestBed.inject(HttpTestingController);
    const supplier = { id: 'supplier-1', name: 'Northstar', contactEmail: null, isActive: true };

    service.list().subscribe();
    const listRequest = http.expectOne('https://api.atlas.test/api/suppliers');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([supplier]);

    service.page(1, 25, 'northstar').subscribe((page) => expect(page).toEqual({ items: [supplier], totalCount: 1 }));
    const pageRequest = http.expectOne((request) => request.url === 'https://api.atlas.test/api/suppliers/page');
    expect(pageRequest.request.params.keys().sort()).toEqual(['pageIndex', 'pageSize', 'search']);
    expect(pageRequest.request.params.get('pageIndex')).toBe('1');
    expect(pageRequest.request.params.get('pageSize')).toBe('25');
    expect(pageRequest.request.params.get('search')).toBe('northstar');
    pageRequest.flush({ items: [supplier], totalCount: 1 });

    service.create({ name: supplier.name, contactEmail: null }).subscribe();
    const createRequest = http.expectOne('https://api.atlas.test/api/suppliers');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush(supplier);

    service.update(supplier.id, { name: supplier.name, contactEmail: null }).subscribe();
    const updateRequest = http.expectOne('https://api.atlas.test/api/suppliers/supplier-1');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(supplier);

    service.activate(supplier.id).subscribe();
    const activateRequest = http.expectOne('https://api.atlas.test/api/suppliers/supplier-1/activate');
    expect(activateRequest.request.method).toBe('POST');
    activateRequest.flush(supplier);

    service.deactivate(supplier.id).subscribe();
    const deactivateRequest = http.expectOne('https://api.atlas.test/api/suppliers/supplier-1/deactivate');
    expect(deactivateRequest.request.method).toBe('POST');
    deactivateRequest.flush(supplier);
    http.verify();
  });
});
