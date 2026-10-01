export interface Supplier {
  id: string;
  name: string;
  contactEmail: string | null;
  isActive: boolean;
}

export interface SupplierRequest {
  name: string;
  contactEmail: string | null;
}
