import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { IncidentService } from './incident.service';

describe('IncidentService', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('uses the configured API base URL for incident operations', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: CHAT_API_CONFIG, useValue: { baseUrl: 'https://api.atlas.test' } }]
    });
    const service = TestBed.inject(IncidentService);
    const http = TestBed.inject(HttpTestingController);
    const incident = createIncident();

    service.list().subscribe();
    const listRequest = http.expectOne('https://api.atlas.test/api/incidents');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([incident]);

    service.get(incident.id).subscribe();
    const getRequest = http.expectOne('https://api.atlas.test/api/incidents/incident-1');
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush(incident);

    service.create({ type: 'Delay', description: 'Inbound delivery delayed.', supplierId: 'supplier-1', purchaseOrderId: null }).subscribe();
    const createRequest = http.expectOne('https://api.atlas.test/api/incidents');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual({ type: 'Delay', description: 'Inbound delivery delayed.', supplierId: 'supplier-1', purchaseOrderId: null });
    createRequest.flush({ id: incident.id });

    service.updateDescription(incident.id, { description: 'Updated incident.' }).subscribe();
    const updateRequest = http.expectOne('https://api.atlas.test/api/incidents/incident-1/description');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(incident);

    service.resolve(incident.id).subscribe();
    const resolveRequest = http.expectOne('https://api.atlas.test/api/incidents/incident-1/resolve');
    expect(resolveRequest.request.method).toBe('POST');
    resolveRequest.flush(incident);
    http.verify();
  });
});

function createIncident() {
  return { id: 'incident-1', type: 'Delay' as const, status: 'Open' as const, supplierId: 'supplier-1', purchaseOrderId: null, description: 'Inbound delivery delayed.', createdAtUtc: '2026-01-01T00:00:00Z', resolvedAtUtc: null, closedAtUtc: null };
}
