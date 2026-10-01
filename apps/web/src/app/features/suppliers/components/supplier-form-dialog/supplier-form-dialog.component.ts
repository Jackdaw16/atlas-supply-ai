import { Component, inject } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Supplier, SupplierRequest } from '../../models/supplier.models';

export interface SupplierFormDialogData {
  supplier?: Supplier;
}

@Component({
  selector: 'app-supplier-form-dialog',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './supplier-form-dialog.component.html',
  styleUrl: './supplier-form-dialog.component.scss'
})
export class SupplierFormDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<SupplierFormDialogComponent, SupplierRequest>);
  protected readonly data = inject<SupplierFormDialogData | null>(MAT_DIALOG_DATA, { optional: true }) ?? {};
  protected readonly isEditMode = this.data.supplier !== undefined;
  protected form: SupplierRequest = {
    name: this.data.supplier?.name ?? '',
    contactEmail: this.data.supplier?.contactEmail ?? null
  };

  protected save(form: NgForm): void {
    const name = this.form.name.trim();
    if (!name) {
      const nameControl = form.controls['name'];
      nameControl?.setErrors({ ...nameControl.errors, whitespace: true });
      return;
    }

    if (form.invalid) {
      return;
    }

    this.dialogRef.close({
      name,
      contactEmail: this.form.contactEmail?.trim() || null
    });
  }
}
