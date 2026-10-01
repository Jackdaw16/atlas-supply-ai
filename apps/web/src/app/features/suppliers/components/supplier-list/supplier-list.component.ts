import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize, Observable } from 'rxjs';
import { Supplier, SupplierRequest } from '../../models/supplier.models';
import { SupplierService } from '../../services/supplier.service';
import { SupplierFormDialogComponent } from '../supplier-form-dialog/supplier-form-dialog.component';

@Component({
  selector: 'app-supplier-list',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule
  ],
  templateUrl: './supplier-list.component.html',
  styleUrl: './supplier-list.component.scss'
})
export class SupplierListComponent {
  private readonly supplierService = inject(SupplierService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly suppliers = signal<Supplier[]>([]);
  protected readonly search = signal('');
  protected readonly selectedSupplier = signal<Supplier | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filteredSuppliers = computed(() => {
    const search = this.search().trim().toLocaleLowerCase();
    if (!search) {
      return this.suppliers();
    }

    return this.suppliers().filter((supplier) =>
      supplier.name.toLocaleLowerCase().includes(search)
      || supplier.contactEmail?.toLocaleLowerCase().includes(search));
  });

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.supplierService.list()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isLoading.set(false))
      )
      .subscribe({
        next: (suppliers) => {
          this.suppliers.set(suppliers);
          const selected = this.selectedSupplier();
          this.selectedSupplier.set(selected ? suppliers.find((supplier) => supplier.id === selected.id) ?? null : null);
        },
        error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'load suppliers'))
      });
  }

  protected createSupplier(): void {
    this.dialog.open(SupplierFormDialogComponent, {
      panelClass: 'supplier-dialog-panel',
      width: '28rem',
      maxHeight: 'calc(100dvh - 2rem)',
      maxWidth: 'calc(100vw - 2rem)'
    }).afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((request: SupplierRequest | undefined) => {
        if (request) {
          this.persist(this.supplierService.create(request), 'Supplier created.');
        }
      });
  }

  protected editSupplier(supplier: Supplier): void {
    this.dialog.open(SupplierFormDialogComponent, {
      data: { supplier },
      panelClass: 'supplier-dialog-panel',
      width: '28rem',
      maxHeight: 'calc(100dvh - 2rem)',
      maxWidth: 'calc(100vw - 2rem)'
    }).afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((request: SupplierRequest | undefined) => {
        if (request) {
          this.persist(this.supplierService.update(supplier.id, request), 'Supplier updated.');
        }
      });
  }

  protected toggleSupplier(supplier: Supplier): void {
    const operation = supplier.isActive
      ? this.supplierService.deactivate(supplier.id)
      : this.supplierService.activate(supplier.id);
    const message = supplier.isActive ? 'Supplier deactivated.' : 'Supplier activated.';

    this.persist(operation, message);
  }

  protected selectSupplier(supplier: Supplier): void {
    this.selectedSupplier.set(supplier);
  }

  private persist(operation: Observable<Supplier>, successMessage: string): void {
    if (this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    operation
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isSaving.set(false))
      )
      .subscribe({
        next: (supplier) => {
          this.suppliers.update((suppliers) => {
            const existingIndex = suppliers.findIndex((item) => item.id === supplier.id);
            return existingIndex === -1
              ? [...suppliers, supplier].sort((left, right) => left.name.localeCompare(right.name))
              : suppliers.map((item) => item.id === supplier.id ? supplier : item);
          });
          if (this.selectedSupplier()?.id === supplier.id) {
            this.selectedSupplier.set(supplier);
          }
          this.snackBar.open(successMessage, 'Dismiss', { duration: 3500 });
        },
        error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'save the supplier'))
      });
  }

  private toErrorMessage(error: unknown, action: string): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The supplier service could not be reached. Check the API connection and try again.';
    }

    return `Unable to ${action}. Please try again.`;
  }
}
