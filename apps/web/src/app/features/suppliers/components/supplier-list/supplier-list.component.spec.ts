import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Supplier } from '../../models/supplier.models';
import { SupplierService } from '../../services/supplier.service';
import { SupplierListComponent } from './supplier-list.component';

describe('SupplierListComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('searches supplier names and emails without matching status labels', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: SupplierService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new SupplierListComponent()) as unknown as {
      suppliers: { set: (suppliers: Supplier[]) => void };
      search: { set: (search: string) => void };
      filteredSuppliers: () => Supplier[];
    };

    component.suppliers.set([
      { id: 'active-supplier', name: 'Northstar', contactEmail: 'orders@northstar.example', isActive: true },
      { id: 'inactive-supplier', name: 'Southwind', contactEmail: 'orders@southwind.example', isActive: false }
    ]);
    component.search.set('active');

    expect(component.filteredSuppliers()).toEqual([]);
    component.search.set('northstar');
    expect(component.filteredSuppliers().map((supplier) => supplier.id)).toEqual(['active-supplier']);
  });

  it('renders accessible icon-only row actions', () => {
    TestBed.configureTestingModule({
      imports: [SupplierListComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: SupplierService,
          useValue: {
            list: () => of([{ id: 'northstar', name: 'Northstar', contactEmail: null, isActive: true }])
          }
        },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });

    const fixture = TestBed.createComponent(SupplierListComponent);
    fixture.detectChanges();

    const actions = fixture.nativeElement.querySelector('.supplier-actions') as HTMLElement;

    expect(actions.querySelector('[aria-label="View Northstar"]')).not.toBeNull();
    expect(actions.querySelector('[aria-label="Edit Northstar"]')).not.toBeNull();
    expect(actions.querySelector('[aria-label="Deactivate Northstar"] mat-icon')?.textContent?.trim()).toBe('toggle_on');
    expect(actions.textContent).not.toContain('Deactivate');
  });

  it('opens a naturally sized dialog constrained to the viewport', () => {
    const dialog = { open: vi.fn(() => ({ afterClosed: () => of(undefined) })) };
    TestBed.configureTestingModule({
      providers: [
        { provide: SupplierService, useValue: { list: () => of([]) } },
        { provide: MatDialog, useValue: dialog },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new SupplierListComponent()) as unknown as {
      createSupplier: () => void;
    };

    component.createSupplier();

    expect(dialog.open).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({
        maxHeight: 'calc(100dvh - 2rem)',
        maxWidth: 'calc(100vw - 2rem)'
      })
    );
  });
});
