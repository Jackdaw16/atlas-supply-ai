import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { PurchaseOrder } from '../../models/purchase-order.models';
import { PurchaseOrderService } from '../../services/purchase-order.service';
import { PurchaseOrderListComponent } from './purchase-order-list.component';

describe('PurchaseOrderListComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('resets to the first page and sends server filters', () => {
    const page = vi.fn(() => of({ items: [], totalCount: 0 }));
    TestBed.configureTestingModule({
      providers: [
        { provide: PurchaseOrderService, useValue: { page } },
        { provide: SupplierService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new PurchaseOrderListComponent()) as unknown as {
      delayedFilter: { set: (filter: 'all' | 'delayed' | 'current') => void };
      pageIndex: { set: (pageIndex: number) => void; (): number };
      updateDelayedFilter: (filter: 'all' | 'delayed' | 'current') => void;
    };

    component.pageIndex.set(3);
    component.updateDelayedFilter('delayed');

    expect(component.pageIndex()).toBe(0);
    expect(page).toHaveBeenLastCalledWith(0, 25, expect.objectContaining({ isDelayed: true }));
  });

  it('renders accessible lifecycle actions with centered icon controls and the shared paginator', () => {
    TestBed.configureTestingModule({
      imports: [PurchaseOrderListComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: PurchaseOrderService,
          useValue: { page: () => of({ items: [createOrder('draft-order', 'supplier-1', false)], totalCount: 1 }) }
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
    expect(fixture.nativeElement.querySelector('app-atlas-paginator')).not.toBeNull();
  });

  it('renders fetched orders with the unknown supplier fallback when supplier lookup fails', () => {
    TestBed.configureTestingModule({
      imports: [PurchaseOrderListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: PurchaseOrderService, useValue: { page: () => of({ items: [createOrder('draft-order', 'missing-supplier', false)], totalCount: 1 }) } },
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
