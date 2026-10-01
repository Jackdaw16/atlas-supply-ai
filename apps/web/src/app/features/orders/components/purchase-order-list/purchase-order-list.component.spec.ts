import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { afterEach, describe, expect, it } from 'vitest';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { PurchaseOrder } from '../../models/purchase-order.models';
import { PurchaseOrderService } from '../../services/purchase-order.service';
import { PurchaseOrderListComponent } from './purchase-order-list.component';

describe('PurchaseOrderListComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('filters only using returned delayed state and selected supplier', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: PurchaseOrderService, useValue: { list: () => of([]) } },
        { provide: SupplierService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new PurchaseOrderListComponent()) as unknown as {
      orders: { set: (orders: PurchaseOrder[]) => void };
      suppliers: { set: (suppliers: { id: string; name: string; contactEmail: null; isActive: boolean }[]) => void };
      delayedFilter: { set: (filter: 'all' | 'delayed' | 'current') => void };
      supplierFilter: { set: (filter: string) => void };
      filteredOrders: () => PurchaseOrder[];
    };
    component.suppliers.set([{ id: 'supplier-1', name: 'Northstar', contactEmail: null, isActive: true }]);
    component.orders.set([
      createOrder('delayed', 'supplier-1', true),
      createOrder('current', 'supplier-2', false)
    ]);

    component.delayedFilter.set('delayed');
    expect(component.filteredOrders().map((order) => order.id)).toEqual(['delayed']);
    component.delayedFilter.set('all');
    component.supplierFilter.set('supplier-1');
    expect(component.filteredOrders().map((order) => order.id)).toEqual(['delayed']);
  });

  it('renders accessible lifecycle actions with centered icon controls', () => {
    TestBed.configureTestingModule({
      imports: [PurchaseOrderListComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: PurchaseOrderService,
          useValue: { list: () => of([createOrder('draft-order', 'supplier-1', false)]) }
        },
        {
          provide: SupplierService,
          useValue: { list: () => of([{ id: 'supplier-1', name: 'Northstar', contactEmail: null, isActive: true }]) }
        },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });

    const fixture = TestBed.createComponent(PurchaseOrderListComponent);
    fixture.detectChanges();

    const actions = fixture.nativeElement.querySelector('.order-actions') as HTMLElement;

    expect(actions.querySelector('[aria-label="View order DRAFT-OR"]')).not.toBeNull();
    expect(actions.querySelector('[aria-label="Edit draft order DRAFT-OR"]')).not.toBeNull();
    expect(actions.querySelector('[aria-label="Submit order DRAFT-OR"]')).not.toBeNull();
    expect(actions.querySelectorAll('.atlas-icon-control')).toHaveLength(4);
  });

  it('renders fetched orders with the unknown supplier fallback when supplier lookup fails', () => {
    TestBed.configureTestingModule({
      imports: [PurchaseOrderListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: PurchaseOrderService, useValue: { list: () => of([createOrder('draft-order', 'missing-supplier', false)]) } },
        { provide: SupplierService, useValue: { list: () => throwError(() => new Error('Supplier lookup failed')) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });

    const fixture = TestBed.createComponent(PurchaseOrderListComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('DRAFT-OR');
    expect(fixture.nativeElement.textContent).toContain('Unknown supplier');
  });
});

function createOrder(id: string, supplierId: string, isDelayed: boolean): PurchaseOrder {
  return { id, supplierId, status: isDelayed ? 'Approved' : 'Draft', createdAtUtc: '2026-01-01T00:00:00Z', submittedAtUtc: null, approvedAtUtc: null, receivedAtUtc: null, items: [], totalAmount: 0, isDelayed };
}
