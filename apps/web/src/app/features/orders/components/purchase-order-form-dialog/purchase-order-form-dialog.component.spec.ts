import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TestBed } from '@angular/core/testing';
import { NgForm } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PurchaseOrderService } from '../../services/purchase-order.service';
import { PurchaseOrderFormDialogComponent } from './purchase-order-form-dialog.component';

describe('PurchaseOrderFormDialogComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('accepts a valid two-decimal money amount', () => {
    const component = createComponent() as unknown as { hasMoneyPrecision: (value: number) => boolean };

    expect(component.hasMoneyPrecision(0.29)).toBe(true);
  });

  it('does not submit an empty draft item list', () => {
    const updateItems = vi.fn();
    const component = createComponent(updateItems) as unknown as {
      items: unknown[];
      backendErrors: () => string[];
      save: (form: NgForm) => void;
    };
    component.items = [];

    component.save({ invalid: false, controls: {} } as unknown as NgForm);

    expect(updateItems).not.toHaveBeenCalled();
    expect(component.backendErrors()).toEqual(['Purchase order requires at least one item.']);
  });
});

function createComponent(updateItems = vi.fn()): PurchaseOrderFormDialogComponent {
  TestBed.configureTestingModule({
    imports: [PurchaseOrderFormDialogComponent],
    providers: [
      provideNoopAnimations(),
      { provide: MAT_DIALOG_DATA, useValue: { suppliers: [], purchaseOrder: createDraftOrder() } },
      { provide: MatDialogRef, useValue: { close: vi.fn() } },
      { provide: PurchaseOrderService, useValue: { updateItems } }
    ]
  });

  return TestBed.createComponent(PurchaseOrderFormDialogComponent).componentInstance;
}

function createDraftOrder() {
  return {
    id: 'draft-order',
    supplierId: 'supplier-1',
    status: 'Draft' as const,
    createdAtUtc: '2026-01-01T00:00:00Z',
    submittedAtUtc: null,
    approvedAtUtc: null,
    receivedAtUtc: null,
    items: [{ id: 'item-1', description: 'Valid item', quantity: 1, unitPrice: 1, lineTotal: 1 }],
    totalAmount: 1,
    isDelayed: false
  };
}
