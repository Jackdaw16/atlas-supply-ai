export type PurchaseOrderStatus = 'Draft' | 'Submitted' | 'Approved' | 'Received' | 'Cancelled';

export interface PurchaseOrderItem {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface PurchaseOrder {
  id: string;
  supplierId: string;
  status: PurchaseOrderStatus;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  receivedAtUtc: string | null;
  items: PurchaseOrderItem[];
  totalAmount: number;
  isDelayed: boolean;
}

export interface PurchaseOrderItemRequest {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  items: PurchaseOrderItemRequest[];
}

export interface UpdatePurchaseOrderItemsRequest {
  items: PurchaseOrderItemRequest[];
}
