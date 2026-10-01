import { TestBed } from '@angular/core/testing';
import { NgForm } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IncidentService } from '../../services/incident.service';
import { IncidentFormDialogComponent } from './incident-form-dialog.component';

describe('IncidentFormDialogComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('filters purchase orders to the selected supplier', () => {
    const component = createComponent() as unknown as { selectSupplier: (supplierId: string) => void; availablePurchaseOrders: () => { id: string }[] };

    component.selectSupplier('supplier-2');

    expect(component.availablePurchaseOrders().map((purchaseOrder) => purchaseOrder.id)).toEqual(['order-2']);
  });

  it('does not save a whitespace-only description', () => {
    const create = vi.fn();
    const component = createComponent(create) as unknown as { description: string; save: (form: NgForm) => void };
    component.description = '   ';

    component.save({ invalid: false, controls: { description: { errors: null, setErrors: vi.fn() } } } as unknown as NgForm);

    expect(create).not.toHaveBeenCalled();
  });
});

function createComponent(create = vi.fn()): IncidentFormDialogComponent {
  TestBed.configureTestingModule({
    imports: [IncidentFormDialogComponent],
    providers: [
      provideNoopAnimations(),
      { provide: MAT_DIALOG_DATA, useValue: { suppliers: [{ id: 'supplier-1', name: 'Northstar', contactEmail: null, isActive: true }, { id: 'supplier-2', name: 'Harbor', contactEmail: null, isActive: true }], purchaseOrders: [createOrder('order-1', 'supplier-1'), createOrder('order-2', 'supplier-2')] } },
      { provide: MatDialogRef, useValue: { close: vi.fn() } },
      { provide: IncidentService, useValue: { create, get: vi.fn(), updateDescription: vi.fn() } }
    ]
  });

  return TestBed.createComponent(IncidentFormDialogComponent).componentInstance;
}

function createOrder(id: string, supplierId: string) {
  return { id, supplierId, status: 'Draft' as const, createdAtUtc: '2026-01-01T00:00:00Z', submittedAtUtc: null, approvedAtUtc: null, receivedAtUtc: null, items: [], totalAmount: 0, isDelayed: false };
}
