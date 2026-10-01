import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { afterEach, describe, expect, it } from 'vitest';
import { PurchaseOrderService } from '../../../orders/services/purchase-order.service';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { Incident } from '../../models/incident.models';
import { IncidentService } from '../../services/incident.service';
import { IncidentListComponent } from './incident-list.component';

describe('IncidentListComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('filters incident results by returned status, supplier, type, and lifecycle state', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: IncidentService, useValue: { list: () => of([]) } },
        { provide: SupplierService, useValue: { list: () => of([]) } },
        { provide: PurchaseOrderService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new IncidentListComponent()) as unknown as {
      incidents: { set: (incidents: Incident[]) => void };
      suppliers: { set: (suppliers: { id: string; name: string; contactEmail: null; isActive: boolean }[]) => void };
      supplierFilter: { set: (filter: string) => void };
      typeFilter: { set: (filter: 'all' | 'Delay' | 'QualityIssue' | 'ShortShipment' | 'DamagedGoods' | 'Other') => void };
      lifecycleFilter: { set: (filter: 'all' | 'open' | 'resolved') => void };
      filteredIncidents: () => Incident[];
    };
    component.suppliers.set([{ id: 'supplier-1', name: 'Northstar', contactEmail: null, isActive: true }]);
    component.incidents.set([createIncident('open-delay', 'supplier-1', 'Delay', 'Open'), createIncident('resolved-quality', 'supplier-2', 'QualityIssue', 'Resolved')]);

    component.supplierFilter.set('supplier-1');
    component.typeFilter.set('Delay');
    expect(component.filteredIncidents().map((incident) => incident.id)).toEqual(['open-delay']);
    component.supplierFilter.set('all');
    component.typeFilter.set('all');
    component.lifecycleFilter.set('resolved');
    expect(component.filteredIncidents().map((incident) => incident.id)).toEqual(['resolved-quality']);
  });

  it('renders select, edit, and resolve controls only for a resolvable incident', () => {
    TestBed.configureTestingModule({
      imports: [IncidentListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: IncidentService, useValue: { list: () => of([createIncident('open-incident', 'supplier-1', 'Delay', 'Open')]) } },
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
  });
});

function createIncident(id: string, supplierId: string, type: Incident['type'], status: Incident['status']): Incident {
  return { id, supplierId, type, status, purchaseOrderId: null, description: 'Inbound delivery delayed.', createdAtUtc: '2026-01-01T00:00:00Z', resolvedAtUtc: null, closedAtUtc: null };
}
