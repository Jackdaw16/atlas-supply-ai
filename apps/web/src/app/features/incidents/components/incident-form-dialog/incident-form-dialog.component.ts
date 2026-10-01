import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { finalize, switchMap } from 'rxjs';
import { PurchaseOrder } from '../../../orders/models/purchase-order.models';
import { Supplier } from '../../../suppliers/models/supplier.models';
import {
  CreateIncidentRequest,
  Incident,
  IncidentType,
  UpdateIncidentDescriptionRequest
} from '../../models/incident.models';
import { IncidentService } from '../../services/incident.service';

export interface IncidentFormDialogData {
  suppliers: Supplier[];
  purchaseOrders: PurchaseOrder[];
  incident?: Incident;
}

@Component({
  selector: 'app-incident-form-dialog',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './incident-form-dialog.component.html',
  styleUrl: './incident-form-dialog.component.scss'
})
export class IncidentFormDialogComponent {
  private readonly incidentService = inject(IncidentService);
  private readonly dialogRef = inject(MatDialogRef<IncidentFormDialogComponent, Incident>);
  protected readonly data = inject<IncidentFormDialogData>(MAT_DIALOG_DATA);
  protected readonly isEditMode = this.data.incident !== undefined;
  protected readonly isSaving = signal(false);
  protected readonly backendErrors = signal<string[]>([]);
  protected readonly incidentTypes: IncidentType[] = ['Delay', 'QualityIssue', 'ShortShipment', 'DamagedGoods', 'Other'];
  protected readonly supplierId = signal(this.data.incident?.supplierId ?? '');
  protected readonly purchaseOrderId = signal(this.data.incident?.purchaseOrderId ?? '');
  protected readonly availablePurchaseOrders = computed(() => this.data.purchaseOrders.filter(
    (purchaseOrder) => purchaseOrder.supplierId === this.supplierId()
  ));
  protected type: IncidentType = this.data.incident?.type ?? 'Delay';
  protected description = this.data.incident?.description ?? '';

  protected selectSupplier(supplierId: string): void {
    this.supplierId.set(supplierId);
    if (!this.availablePurchaseOrders().some((purchaseOrder) => purchaseOrder.id === this.purchaseOrderId())) {
      this.purchaseOrderId.set('');
    }
  }

  protected shortId(id: string): string {
    return id.slice(0, 8).toUpperCase();
  }

  protected supplierName(supplierId: string): string {
    return this.data.suppliers.find((supplier) => supplier.id === supplierId)?.name ?? 'Unknown supplier';
  }

  protected save(form: NgForm): void {
    this.backendErrors.set([]);
    const description = this.description.trim();
    if (!description) {
      form.controls['description']?.setErrors({ ...form.controls['description']?.errors, whitespace: true });
      return;
    }

    if (!this.isEditMode && !this.supplierId()) {
      form.controls['supplierId']?.setErrors({ ...form.controls['supplierId']?.errors, required: true });
      return;
    }

    if (form.invalid) {
      return;
    }

    const operation = this.isEditMode
      ? this.incidentService.updateDescription(this.data.incident!.id, { description } satisfies UpdateIncidentDescriptionRequest)
      : this.incidentService.create({
        type: this.type,
        description,
        supplierId: this.supplierId(),
        purchaseOrderId: this.purchaseOrderId() || null
      } satisfies CreateIncidentRequest).pipe(switchMap((incident) => this.incidentService.get(incident.id)));

    this.isSaving.set(true);
    operation.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: (incident) => this.dialogRef.close(incident),
      error: (error: unknown) => this.backendErrors.set(this.toValidationErrors(error))
    });
  }

  private toValidationErrors(error: unknown): string[] {
    if (error instanceof HttpErrorResponse && error.error?.errors) {
      return Object.values(error.error.errors).flat() as string[];
    }

    return ['Unable to save the incident. Review the values and try again.'];
  }
}
