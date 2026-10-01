import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';
import { Supplier } from '../../../suppliers/models/supplier.models';
import {
  CreatePurchaseOrderRequest,
  PurchaseOrder,
  PurchaseOrderItemRequest,
  UpdatePurchaseOrderItemsRequest
} from '../../models/purchase-order.models';
import { PurchaseOrderService } from '../../services/purchase-order.service';

export interface PurchaseOrderFormDialogData {
  suppliers: Supplier[];
  purchaseOrder?: PurchaseOrder;
}

@Component({
  selector: 'app-purchase-order-form-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule
  ],
  templateUrl: './purchase-order-form-dialog.component.html',
  styleUrl: './purchase-order-form-dialog.component.scss'
})
export class PurchaseOrderFormDialogComponent {
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly dialogRef = inject(MatDialogRef<PurchaseOrderFormDialogComponent, PurchaseOrder>);
  protected readonly data = inject<PurchaseOrderFormDialogData>(MAT_DIALOG_DATA);
  protected readonly isEditMode = this.data.purchaseOrder !== undefined;
  protected readonly isSaving = signal(false);
  protected readonly backendErrors = signal<string[]>([]);
  protected supplierId = this.data.purchaseOrder?.supplierId ?? '';
  protected items: PurchaseOrderItemRequest[] = this.data.purchaseOrder?.items.map((item) => ({
    description: item.description,
    quantity: item.quantity,
    unitPrice: item.unitPrice
  })) ?? [{ description: '', quantity: 1, unitPrice: 0 }];

  protected addItem(): void {
    this.items = [...this.items, { description: '', quantity: 1, unitPrice: 0 }];
  }

  protected removeItem(index: number): void {
    if (this.items.length > 1) {
      this.items = this.items.filter((_, itemIndex) => itemIndex !== index);
    }
  }

  protected save(form: NgForm): void {
    this.backendErrors.set([]);
    if (this.items.length === 0) {
      this.backendErrors.set(['Purchase order requires at least one item.']);
      return;
    }

    if (!this.supplierId) {
      const supplierControl = form.controls['supplierId'];
      supplierControl?.setErrors({ ...supplierControl.errors, required: true });
      return;
    }

    if (form.invalid || this.items.some((item) => !item.description.trim() || item.quantity <= 0 || item.unitPrice <= 0 || !this.hasMoneyPrecision(item.unitPrice))) {
      return;
    }

    const items = this.items.map((item) => ({ ...item, description: item.description.trim() }));
    const operation = this.isEditMode
      ? this.purchaseOrderService.updateItems(this.data.purchaseOrder!.id, { items } satisfies UpdatePurchaseOrderItemsRequest)
      : this.purchaseOrderService.create({ supplierId: this.supplierId, items } satisfies CreatePurchaseOrderRequest);

    this.isSaving.set(true);
    operation.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: (purchaseOrder) => this.dialogRef.close(purchaseOrder),
      error: (error: unknown) => this.backendErrors.set(this.toValidationErrors(error))
    });
  }

  protected hasMoneyPrecision(value: number): boolean {
    return Number.isFinite(value) && value > 0 && value <= 9_999_999_999_999_999.99 && Number(value.toFixed(2)) === value;
  }

  private toValidationErrors(error: unknown): string[] {
    if (error instanceof HttpErrorResponse && error.error?.errors) {
      return Object.values(error.error.errors).flat() as string[];
    }

    return ['Unable to save the order. Review the values and try again.'];
  }
}
