import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import {
  CreateIncidentRequest,
  CreateIncidentResponse,
  Incident,
  UpdateIncidentDescriptionRequest
} from '../models/incident.models';

@Injectable({ providedIn: 'root' })
export class IncidentService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(CHAT_API_CONFIG);

  list() {
    return this.http.get<Incident[]>(`${this.apiConfig.baseUrl}/api/incidents`);
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
