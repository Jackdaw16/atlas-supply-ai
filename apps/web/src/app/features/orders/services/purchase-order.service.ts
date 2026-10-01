import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import {
  CreatePurchaseOrderRequest,
  PurchaseOrder,
  UpdatePurchaseOrderItemsRequest
} from '../models/purchase-order.models';
import { PagedResponse } from '../../../core/models/paged-response.model';

export interface PurchaseOrderPageQuery {
  search: string;
  status?: string;
  supplierId?: string;
  isDelayed?: boolean;
}

@Injectable({ providedIn: 'root' })
export class PurchaseOrderService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(CHAT_API_CONFIG);

  list() {
    return this.http.get<PurchaseOrder[]>(`${this.apiConfig.baseUrl}/api/orders`);
  }

  page(pageIndex: number, pageSize: number, query: PurchaseOrderPageQuery) {
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
    if (query.isDelayed !== undefined) {
      params = params.set('isDelayed', query.isDelayed);
    }

    return this.http.get<PagedResponse<PurchaseOrder>>(`${this.apiConfig.baseUrl}/api/orders/page`, { params });
  }

  get(id: string) {
    return this.http.get<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders/${id}`);
  }

  create(request: CreatePurchaseOrderRequest) {
    return this.http.post<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders`, request);
  }

  updateItems(id: string, request: UpdatePurchaseOrderItemsRequest) {
    return this.http.put<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders/${id}/items`, request);
  }

  submit(id: string) {
    return this.http.post<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders/${id}/submit`, {});
  }

  approve(id: string) {
    return this.http.post<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders/${id}/approve`, {});
  }

  markReceived(id: string) {
    return this.http.post<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders/${id}/receive`, {});
  }

  cancel(id: string) {
    return this.http.post<PurchaseOrder>(`${this.apiConfig.baseUrl}/api/orders/${id}/cancel`, {});
  }
}
