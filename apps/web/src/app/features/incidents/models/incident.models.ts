export type IncidentType = 'Delay' | 'QualityIssue' | 'ShortShipment' | 'DamagedGoods' | 'Other';

export type IncidentStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';

export interface Incident {
  id: string;
  type: IncidentType;
  status: IncidentStatus;
  supplierId: string;
  purchaseOrderId: string | null;
  description: string;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
}

export interface CreateIncidentRequest {
  type: IncidentType;
  description: string;
  supplierId: string;
  purchaseOrderId: string | null;
}

export interface CreateIncidentResponse {
  id: string;
}

export interface UpdateIncidentDescriptionRequest {
  description: string;
}
