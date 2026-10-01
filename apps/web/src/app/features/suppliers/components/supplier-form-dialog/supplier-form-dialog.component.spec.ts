import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TestBed } from '@angular/core/testing';
import { NgForm } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SupplierFormDialogComponent } from './supplier-form-dialog.component';

describe('SupplierFormDialogComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('renders in create mode without dialog data', () => {
    const dialogRef = { close: vi.fn() };
    TestBed.configureTestingModule({
      imports: [SupplierFormDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    });

    const fixture = TestBed.createComponent(SupplierFormDialogComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Add supplier');
    expect(fixture.nativeElement.querySelector('input[name="name"]')).not.toBeNull();
  });

  it('keeps the dialog open when the name contains only whitespace', () => {
    const dialogRef = { close: vi.fn() };
    TestBed.configureTestingModule({
      imports: [SupplierFormDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    });

    const fixture = TestBed.createComponent(SupplierFormDialogComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as {
      form: { name: string };
      save: (form: NgForm) => void;
    };
    const nameControl = { errors: null, setErrors: vi.fn() };

    component.form.name = '   ';
    component.save({ invalid: false, controls: { name: nameControl } } as unknown as NgForm);

    expect(nameControl.setErrors).toHaveBeenCalledWith({ whitespace: true });
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
