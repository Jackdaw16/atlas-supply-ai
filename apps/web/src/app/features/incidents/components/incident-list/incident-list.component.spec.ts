import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PurchaseOrderService } from '../../../orders/services/purchase-order.service';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { Incident } from '../../models/incident.models';
import { IncidentService } from '../../services/incident.service';
import { IncidentListComponent } from './incident-list.component';

describe('IncidentListComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('resets to the first page and sends lifecycle filtering to the server', () => {
    const page = vi.fn(() => of({ items: [], totalCount: 0 }));
    TestBed.configureTestingModule({
      providers: [
        { provide: IncidentService, useValue: { page } },
        { provide: SupplierService, useValue: { list: () => of([]) } },
        { provide: PurchaseOrderService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new IncidentListComponent()) as unknown as {
      pageIndex: { set: (pageIndex: number) => void; (): number };
      updateLifecycleFilter: (filter: 'all' | 'open' | 'resolved') => void;
    };

    component.pageIndex.set(4);
    component.updateLifecycleFilter('resolved');

    expect(component.pageIndex()).toBe(0);
    expect(page).toHaveBeenLastCalledWith(0, 25, expect.objectContaining({ lifecycle: 'resolved' }));
  });

  it('renders select, edit, and resolve controls with the shared paginator for a resolvable incident', () => {
    TestBed.configureTestingModule({
      imports: [IncidentListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: IncidentService, useValue: { page: () => of({ items: [createIncident('open-incident', 'supplier-1', 'Delay', 'Open')], totalCount: 1 }) } },
        { provide: SupplierService, useValue: { list: () => of([{ id: 'supplier-1', name: 'Northstar', contactEmail: null, isActive: true }]) } },
        { provide: PurchaseOrderService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });

    const fixture = TestBed.createComponent(IncidentListComponent);
    fixture.detectChanges();

    const actions = fixture.nativeElement.querySelector('.incident-actions') as HTMLElement;
    expect(actions.querySelector('[aria-label="View incident OPEN-INC"]')).not.toBeNull();
    expect(actions.querySelector('[aria-label="Edit incident OPEN-INC"]')).not.toBeNull();
    expect(actions.querySelector('[aria-label="Resolve incident OPEN-INC"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-atlas-paginator')).not.toBeNull();
  });
});

function createIncident(id: string, supplierId: string, type: Incident['type'], status: Incident['status']): Incident {
  return { id, supplierId, type, status, purchaseOrderId: null, description: 'Inbound delivery delayed.', createdAtUtc: '2026-01-01T00:00:00Z', resolvedAtUtc: null, closedAtUtc: null };
}
