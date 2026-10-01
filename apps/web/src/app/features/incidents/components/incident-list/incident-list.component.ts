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
import { PurchaseOrder } from '../../../orders/models/purchase-order.models';
import { PurchaseOrderService } from '../../../orders/services/purchase-order.service';
import { Supplier } from '../../../suppliers/models/supplier.models';
import { SupplierService } from '../../../suppliers/services/supplier.service';
import { Incident, IncidentStatus, IncidentType } from '../../models/incident.models';
import { IncidentService } from '../../services/incident.service';
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
    MatTooltipModule
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
  protected readonly statusOptions: IncidentStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed', 'Cancelled'];
  protected readonly typeOptions: IncidentType[] = ['Delay', 'QualityIssue', 'ShortShipment', 'DamagedGoods', 'Other'];
  protected readonly filteredIncidents = computed(() => {
    const search = this.search().trim().toLocaleLowerCase();
    return this.incidents().filter((incident) => {
      const matchesSearch = !search
        || incident.id.toLocaleLowerCase().includes(search)
        || incident.description.toLocaleLowerCase().includes(search)
        || this.supplierName(incident.supplierId).toLocaleLowerCase().includes(search);
      const matchesStatus = this.statusFilter() === 'all' || incident.status === this.statusFilter();
      const matchesSupplier = this.supplierFilter() === 'all' || incident.supplierId === this.supplierFilter();
      const matchesType = this.typeFilter() === 'all' || incident.type === this.typeFilter();
      const matchesLifecycle = this.lifecycleFilter() === 'all'
        || (this.lifecycleFilter() === 'open' ? incident.status === 'Open' : incident.status === 'Resolved');
      return matchesSearch && matchesStatus && matchesSupplier && matchesType && matchesLifecycle;
    });
  });

  constructor() {
    this.reload();
  }

  protected reload(selectedIncidentId = this.selectedIncident()?.id): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      incidents: this.incidentService.list(),
      suppliers: this.supplierService.list().pipe(catchError(() => of([]))),
      purchaseOrders: this.purchaseOrderService.list().pipe(catchError(() => of([])))
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ incidents, suppliers, purchaseOrders }) => {
          this.incidents.set(incidents);
          this.suppliers.set(suppliers);
          this.purchaseOrders.set(purchaseOrders);
          this.selectedIncident.set(selectedIncidentId ? incidents.find((incident) => incident.id === selectedIncidentId) ?? null : null);
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

  private toErrorMessage(error: unknown, action: string): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'The incident service could not be reached. Check the API connection and try again.';
    }

    return `Unable to ${action}. Please try again.`;
  }
}
