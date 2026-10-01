import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
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
import { AtlasPaginatorComponent, PaginationChange } from '../../../../shared/components/paginator/atlas-paginator.component';
import { PurchaseOrder, PurchaseOrderStatus } from '../../models/purchase-order.models';
import { PurchaseOrderPageQuery, PurchaseOrderService } from '../../services/purchase-order.service';
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
    MatTooltipModule,
    AtlasPaginatorComponent
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
  protected readonly totalCount = signal(0);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(25);
  protected readonly statusOptions: PurchaseOrderStatus[] = ['Draft', 'Submitted', 'Approved', 'Received', 'Cancelled'];

  constructor() {
    this.reload();
  }

  protected reload(selectedOrderId = this.selectedOrder()?.id): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      page: this.purchaseOrderService.page(this.pageIndex(), this.pageSize(), this.pageQuery()),
      suppliers: this.supplierService.list().pipe(catchError(() => of([])))
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ page, suppliers }) => {
          this.orders.set(page.items);
          this.totalCount.set(page.totalCount);
          this.suppliers.set(suppliers);
          this.selectedOrder.set(selectedOrderId ? page.items.find((order) => order.id === selectedOrderId) ?? null : null);
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

  protected updateSearch(search: string): void {
    this.search.set(search);
    this.resetPageAndReload();
  }

  protected updateStatusFilter(status: PurchaseOrderStatus | 'all'): void {
    this.statusFilter.set(status);
    this.resetPageAndReload();
  }

  protected updateSupplierFilter(supplierId: string): void {
    this.supplierFilter.set(supplierId);
    this.resetPageAndReload();
  }

  protected updateDelayedFilter(filter: 'all' | 'delayed' | 'current'): void {
    this.delayedFilter.set(filter);
    this.resetPageAndReload();
  }

  protected changePage(event: PaginationChange): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.reload();
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
          this.reload(saved.id);
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
        this.reload(purchaseOrder.id);
        this.snackBar.open(successMessage, 'Dismiss', { duration: 3500 });
      },
      error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'update the order'))
    });
  }

  private pageQuery(): PurchaseOrderPageQuery {
    const status = this.statusFilter();
    const supplierId = this.supplierFilter();
    const delayed = this.delayedFilter();
    return {
      search: this.search(),
      status: status === 'all' ? undefined : status,
      supplierId: supplierId === 'all' ? undefined : supplierId,
      isDelayed: delayed === 'all' ? undefined : delayed === 'delayed'
    };
  }

  private resetPageAndReload(): void {
    this.pageIndex.set(0);
    this.reload();
  }

  private toErrorMessage(error: unknown, action: string): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The order service could not be reached. Check the API connection and try again.';
    }

    return `Unable to ${action}. Please try again.`;
  }
}
