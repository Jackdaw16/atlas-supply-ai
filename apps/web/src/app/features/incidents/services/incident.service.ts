import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import {
  CreateIncidentRequest,
  CreateIncidentResponse,
  Incident,
  UpdateIncidentDescriptionRequest
} from '../models/incident.models';
import { PagedResponse } from '../../../core/models/paged-response.model';

export interface IncidentPageQuery {
  search: string;
  status?: string;
  supplierId?: string;
  type?: string;
  lifecycle?: 'open' | 'resolved';
}

@Injectable({ providedIn: 'root' })
export class IncidentService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(CHAT_API_CONFIG);

  list() {
    return this.http.get<Incident[]>(`${this.apiConfig.baseUrl}/api/incidents`);
  }

  page(pageIndex: number, pageSize: number, query: IncidentPageQuery) {
    let params = new HttpParams()
      .set('pageIndex', pageIndex)
      .set('pageSize', pageSize)
      .set('search', query.search);

    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.supplierId) {
      params = params.set('supplierId', query.supplierId);
    }
    if (query.type) {
      params = params.set('type', query.type);
    }
    if (query.lifecycle) {
      params = params.set('lifecycle', query.lifecycle);
    }

    return this.http.get<PagedResponse<Incident>>(`${this.apiConfig.baseUrl}/api/incidents/page`, { params });
  }

  get(id: string) {
    return this.http.get<Incident>(`${this.apiConfig.baseUrl}/api/incidents/${id}`);
  }

  create(request: CreateIncidentRequest) {
    return this.http.post<CreateIncidentResponse>(`${this.apiConfig.baseUrl}/api/incidents`, request);
  }

  updateDescription(id: string, request: UpdateIncidentDescriptionRequest) {
    return this.http.put<Incident>(`${this.apiConfig.baseUrl}/api/incidents/${id}/description`, request);
  }

  resolve(id: string) {
    return this.http.post<Incident>(`${this.apiConfig.baseUrl}/api/incidents/${id}/resolve`, {});
  }
}
