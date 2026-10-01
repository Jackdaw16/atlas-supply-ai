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

  it('resets to the first page when search changes', () => {
    const page = vi.fn(() => of({ items: [], totalCount: 0 }));
    TestBed.configureTestingModule({
      providers: [
        { provide: SupplierService, useValue: { page } },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new SupplierListComponent()) as unknown as {
      pageIndex: { set: (pageIndex: number) => void; (): number };
      updateSearch: (search: string) => void;
    };

    component.pageIndex.set(2);
    component.updateSearch('northstar');

    expect(component.pageIndex()).toBe(0);
    expect(page).toHaveBeenLastCalledWith(0, 25, 'northstar');
  });

  it('renders accessible icon-only row actions and the shared paginator', () => {
    TestBed.configureTestingModule({
      imports: [SupplierListComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: SupplierService,
          useValue: { page: () => of({ items: [{ id: 'northstar', name: 'Northstar', contactEmail: null, isActive: true }], totalCount: 1 }) }
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
    expect(fixture.nativeElement.querySelector('app-atlas-paginator')).not.toBeNull();
  });

  it('opens a naturally sized dialog constrained to the viewport', () => {
    const dialog = { open: vi.fn(() => ({ afterClosed: () => of(undefined) })) };
    TestBed.configureTestingModule({
      providers: [
        { provide: SupplierService, useValue: { page: () => of({ items: [], totalCount: 0 }) } },
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
