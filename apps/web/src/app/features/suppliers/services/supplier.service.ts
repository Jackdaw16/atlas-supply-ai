import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { Supplier, SupplierRequest } from '../models/supplier.models';
import { PagedResponse } from '../../../core/models/paged-response.model';

@Injectable({ providedIn: 'root' })
export class SupplierService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(CHAT_API_CONFIG);

  list() {
    return this.http.get<Supplier[]>(`${this.apiConfig.baseUrl}/api/suppliers`);
  }

  page(pageIndex: number, pageSize: number, search = '') {
    const params = new HttpParams()
      .set('pageIndex', pageIndex)
      .set('pageSize', pageSize)
      .set('search', search);
    return this.http.get<PagedResponse<Supplier>>(`${this.apiConfig.baseUrl}/api/suppliers/page`, { params });
  }

  create(request: SupplierRequest) {
    return this.http.post<Supplier>(`${this.apiConfig.baseUrl}/api/suppliers`, request);
  }

  update(id: string, request: SupplierRequest) {
    return this.http.put<Supplier>(`${this.apiConfig.baseUrl}/api/suppliers/${id}`, request);
  }

  activate(id: string) {
    return this.http.post<Supplier>(`${this.apiConfig.baseUrl}/api/suppliers/${id}/activate`, {});
  }

  deactivate(id: string) {
    return this.http.post<Supplier>(`${this.apiConfig.baseUrl}/api/suppliers/${id}/deactivate`, {});
  }
}
