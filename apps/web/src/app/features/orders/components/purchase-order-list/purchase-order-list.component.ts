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
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, finalize, forkJoin, Observable, of } from 'rxjs';
import { Supplier } from '../../../suppliers/models/supplier.models';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { PurchaseOrder, PurchaseOrderStatus } from '../../models/purchase-order.models';
import { PurchaseOrderService } from '../../services/purchase-order.service';
import { PurchaseOrderFormDialogComponent } from '../purchase-order-form-dialog/purchase-order-form-dialog.component';

@Component({
  selector: 'app-purchase-order-list',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTooltipModule
  ],
  templateUrl: './purchase-order-list.component.html',
  styleUrl: './purchase-order-list.component.scss'
})
export class PurchaseOrderListComponent {
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly supplierService = inject(SupplierService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly orders = signal<PurchaseOrder[]>([]);
  protected readonly suppliers = signal<Supplier[]>([]);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<PurchaseOrderStatus | 'all'>('all');
  protected readonly supplierFilter = signal('all');
  protected readonly delayedFilter = signal<'all' | 'delayed' | 'current'>('all');
  protected readonly selectedOrder = signal<PurchaseOrder | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly statusOptions: PurchaseOrderStatus[] = ['Draft', 'Submitted', 'Approved', 'Received', 'Cancelled'];
  protected readonly filteredOrders = computed(() => {
    const search = this.search().trim().toLocaleLowerCase();
    return this.orders().filter((order) => {
      const matchesSearch = !search
        || order.id.toLocaleLowerCase().includes(search)
        || this.supplierName(order.supplierId).toLocaleLowerCase().includes(search);
      const matchesStatus = this.statusFilter() === 'all' || order.status === this.statusFilter();
      const matchesSupplier = this.supplierFilter() === 'all' || order.supplierId === this.supplierFilter();
      const matchesDelayed = this.delayedFilter() === 'all'
        || (this.delayedFilter() === 'delayed' ? order.isDelayed : !order.isDelayed);
      return matchesSearch && matchesStatus && matchesSupplier && matchesDelayed;
    });
  });

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      orders: this.purchaseOrderService.list(),
      suppliers: this.supplierService.list().pipe(catchError(() => of([])))
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ orders, suppliers }) => {
          this.orders.set(orders);
          this.suppliers.set(suppliers);
          this.refreshSelectedOrder();
        },
        error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'load orders'))
      });
  }

  protected createOrder(): void {
    this.openForm();
  }

  protected editOrder(order: PurchaseOrder): void {
    if (order.status === 'Draft') {
      this.openForm(order);
    }
  }

  protected selectOrder(order: PurchaseOrder): void {
    this.selectedOrder.set(order);
  }

  protected transition(order: PurchaseOrder, action: 'submit' | 'approve' | 'receive' | 'cancel'): void {
    const operation = action === 'submit' ? this.purchaseOrderService.submit(order.id)
      : action === 'approve' ? this.purchaseOrderService.approve(order.id)
        : action === 'receive' ? this.purchaseOrderService.markReceived(order.id)
          : this.purchaseOrderService.cancel(order.id);
    const label = action === 'submit' ? 'Order submitted.'
      : action === 'approve' ? 'Order approved.'
        : action === 'receive' ? 'Order marked as received.'
          : 'Order cancelled.';
    this.persist(operation, label);
  }

  protected supplierName(supplierId: string): string {
    return this.suppliers().find((supplier) => supplier.id === supplierId)?.name ?? 'Unknown supplier';
  }

  protected shortId(order: PurchaseOrder): string {
    return order.id.slice(0, 8).toUpperCase();
  }

  protected formatDate(value: string | null): string {
    return value ? new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—';
  }

  protected formatAmount(value: number): string {
    return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  protected canCancel(order: PurchaseOrder): boolean {
    return order.status !== 'Received' && order.status !== 'Cancelled';
  }

  private openForm(purchaseOrder?: PurchaseOrder): void {
    this.dialog.open(PurchaseOrderFormDialogComponent, {
      data: { suppliers: this.suppliers(), purchaseOrder },
      panelClass: 'order-dialog-panel',
      width: '42rem',
      maxHeight: 'calc(100dvh - 2rem)',
      maxWidth: 'calc(100vw - 2rem)'
    }).afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((saved: PurchaseOrder | undefined) => {
        if (saved) {
          this.upsert(saved);
          this.snackBar.open(purchaseOrder ? 'Draft order updated.' : 'Order created.', 'Dismiss', { duration: 3500 });
        }
      });
  }

  private persist(operation: Observable<PurchaseOrder>, successMessage: string): void {
    if (this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    operation.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isSaving.set(false))).subscribe({
      next: (purchaseOrder) => {
        this.upsert(purchaseOrder);
        this.snackBar.open(successMessage, 'Dismiss', { duration: 3500 });
      },
      error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'update the order'))
    });
  }

  private upsert(purchaseOrder: PurchaseOrder): void {
    this.orders.update((orders) => {
      const found = orders.some((order) => order.id === purchaseOrder.id);
      const updated = found ? orders.map((order) => order.id === purchaseOrder.id ? purchaseOrder : order) : [...orders, purchaseOrder];
      return updated.sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc));
    });
    this.selectedOrder.set(purchaseOrder);
  }

  private refreshSelectedOrder(): void {
    const selected = this.selectedOrder();
    this.selectedOrder.set(selected ? this.orders().find((order) => order.id === selected.id) ?? null : null);
  }

  private toErrorMessage(error: unknown, action: string): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The order service could not be reached. Check the API connection and try again.';
    }

    return `Unable to ${action}. Please try again.`;
  }
}
