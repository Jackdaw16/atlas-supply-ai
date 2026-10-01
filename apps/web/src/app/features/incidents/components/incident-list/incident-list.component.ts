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
import { PurchaseOrder } from '../../../orders/models/purchase-order.models';
import { PurchaseOrderService } from '../../../orders/services/purchase-order.service';
import { Supplier } from '../../../suppliers/models/supplier.models';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { AtlasPaginatorComponent, PaginationChange } from '../../../../shared/components/paginator/atlas-paginator.component';
import { Incident, IncidentStatus, IncidentType } from '../../models/incident.models';
import { IncidentPageQuery, IncidentService } from '../../services/incident.service';
import { IncidentFormDialogComponent } from '../incident-form-dialog/incident-form-dialog.component';

@Component({
  selector: 'app-incident-list',
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
  templateUrl: './incident-list.component.html',
  styleUrl: './incident-list.component.scss'
})
export class IncidentListComponent {
  private readonly incidentService = inject(IncidentService);
  private readonly supplierService = inject(SupplierService);
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly incidents = signal<Incident[]>([]);
  protected readonly suppliers = signal<Supplier[]>([]);
  protected readonly purchaseOrders = signal<PurchaseOrder[]>([]);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<IncidentStatus | 'all'>('all');
  protected readonly supplierFilter = signal('all');
  protected readonly typeFilter = signal<IncidentType | 'all'>('all');
  protected readonly lifecycleFilter = signal<'all' | 'open' | 'resolved'>('all');
  protected readonly selectedIncident = signal<Incident | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(25);
  protected readonly statusOptions: IncidentStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed', 'Cancelled'];
  protected readonly typeOptions: IncidentType[] = ['Delay', 'QualityIssue', 'ShortShipment', 'DamagedGoods', 'Other'];

  constructor() {
    this.reload();
  }

  protected reload(selectedIncidentId = this.selectedIncident()?.id): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      page: this.incidentService.page(this.pageIndex(), this.pageSize(), this.pageQuery()),
      suppliers: this.supplierService.list().pipe(catchError(() => of([]))),
      purchaseOrders: this.purchaseOrderService.list().pipe(catchError(() => of([])))
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ page, suppliers, purchaseOrders }) => {
          this.incidents.set(page.items);
          this.totalCount.set(page.totalCount);
          this.suppliers.set(suppliers);
          this.purchaseOrders.set(purchaseOrders);
          this.selectedIncident.set(selectedIncidentId ? page.items.find((incident) => incident.id === selectedIncidentId) ?? null : null);
        },
        error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'load incidents'))
      });
  }

  protected createIncident(): void {
    this.openForm();
  }

  protected editIncident(incident: Incident): void {
    this.openForm(incident);
  }

  protected selectIncident(incident: Incident): void {
    this.selectedIncident.set(incident);
  }

  protected updateSearch(search: string): void {
    this.search.set(search);
    this.resetPageAndReload();
  }

  protected updateStatusFilter(status: IncidentStatus | 'all'): void {
    this.statusFilter.set(status);
    this.resetPageAndReload();
  }

  protected updateSupplierFilter(supplierId: string): void {
    this.supplierFilter.set(supplierId);
    this.resetPageAndReload();
  }

  protected updateTypeFilter(type: IncidentType | 'all'): void {
    this.typeFilter.set(type);
    this.resetPageAndReload();
  }

  protected updateLifecycleFilter(filter: 'all' | 'open' | 'resolved'): void {
    this.lifecycleFilter.set(filter);
    this.resetPageAndReload();
  }

  protected changePage(event: PaginationChange): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.reload();
  }

  protected resolveIncident(incident: Incident): void {
    this.persist(this.incidentService.resolve(incident.id), 'Incident resolved.');
  }

  protected supplierName(supplierId: string): string {
    return this.suppliers().find((supplier) => supplier.id === supplierId)?.name ?? 'Unknown supplier';
  }

  protected shortId(id: string): string {
    return id.slice(0, 8).toUpperCase();
  }

  protected formatDate(value: string | null): string {
    return value ? new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—';
  }

  protected canResolve(incident: Incident): boolean {
    return incident.status === 'Open' || incident.status === 'InProgress';
  }

  private openForm(incident?: Incident): void {
    this.dialog.open(IncidentFormDialogComponent, {
      data: { suppliers: this.suppliers(), purchaseOrders: this.purchaseOrders(), incident },
      panelClass: 'incident-dialog-panel',
      width: '38rem',
      maxHeight: 'calc(100dvh - 2rem)',
      maxWidth: 'calc(100vw - 2rem)'
    }).afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((saved: Incident | undefined) => {
        if (saved) {
          this.reload(saved.id);
          this.snackBar.open(incident ? 'Incident description updated.' : 'Incident created.', 'Dismiss', { duration: 3500 });
        }
      });
  }

  private persist(operation: Observable<Incident>, successMessage: string): void {
    if (this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    operation.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isSaving.set(false))).subscribe({
      next: (incident) => {
        this.reload(incident.id);
        this.snackBar.open(successMessage, 'Dismiss', { duration: 3500 });
      },
      error: (error: unknown) => this.errorMessage.set(this.toErrorMessage(error, 'update the incident'))
    });
  }

  private pageQuery(): IncidentPageQuery {
    const status = this.statusFilter();
    const supplierId = this.supplierFilter();
    const type = this.typeFilter();
    const lifecycle = this.lifecycleFilter();
    return {
      search: this.search(),
      status: status === 'all' ? undefined : status,
      supplierId: supplierId === 'all' ? undefined : supplierId,
      type: type === 'all' ? undefined : type,
      lifecycle: lifecycle === 'all' ? undefined : lifecycle
    };
  }

  private resetPageAndReload(): void {
    this.pageIndex.set(0);
    this.reload();
  }

  private toErrorMessage(error: unknown, action: string): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The incident service could not be reached. Check the API connection and try again.';
    }

    return `Unable to ${action}. Please try again.`;
  }
}
