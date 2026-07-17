import { Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { NhanVienManagementService } from '../Services/nhan-vien-management.service';

@Component({
  selector: 'app-nhan-vien-management-edit-dialog',
  templateUrl: './nhan-vien-management-edit-dialog.component.html',
})
export class NhanVienManagementEditDialogComponent implements OnInit {
  formGroup!: FormGroup;
  apiError = '';
  private readonly serverErrors: { [key: string]: string } = {};

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    public dialogRef: MatDialogRef<NhanVienManagementEditDialogComponent>,
    private fb: FormBuilder,
    private service: NhanVienManagementService
  ) {}

  ngOnInit(): void {
    this.formGroup = this.fb.group({
      MaNV: ['', [Validators.required, Validators.pattern(/^NV\d{1,10}$/i)]],
      HoTen: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      SDT: ['', Validators.pattern(/^0\d{9}$/)],
      CCCD: ['', [Validators.required, Validators.pattern(/^(\d{9}|\d{12})$/)]],
      Email: ['', [Validators.email, Validators.maxLength(100)]],
      DiaChi: ['', Validators.maxLength(255)],
      PhongBan: ['', Validators.pattern(/^[1-9]\d*$/)],
      ChucVu: ['', Validators.maxLength(100)],
    });

    if (this.data?.item) {
      this.formGroup.patchValue(this.data.item);
    }

    Object.keys(this.formGroup.controls).forEach((name) => {
      this.formGroup.get(name)?.valueChanges.subscribe(() => delete this.serverErrors[name]);
    });
  }

  onSubmit(): void {
    if (this.formGroup.invalid) {
      this.formGroup.markAllAsTouched();
      return;
    }

    const data = this.formGroup.value;

    if (this.data?.item?.Id) {
      data.Id = this.data.item.Id;

      this.service.updateNhanVien(data).subscribe(
        (res) => this.handleResponse(res),
        (error) => this.showError(error)
      );
    } else {
      this.service.createNhanVien(data).subscribe(
        (res) => this.handleResponse(res),
        (error) => this.showError(error)
      );
    }
  }

  private handleResponse(res: any): void {
    if (res?.status === 1) {
      this.dialogRef.close(res);
      return;
    }

    this.showApiMessage(res?.error?.message || 'Không thể lưu nhân viên.');
  }

  private showError(error: any): void {
    this.showApiMessage(error?.error?.error?.message || error?.error?.message || 'Không thể kết nối API.');
  }

  hasError(controlName: string): boolean {
    const control = this.formGroup.get(controlName);
    return !!(this.serverErrors[controlName] || (control && control.invalid && (control.touched || control.dirty)));
  }

  getErrorMessage(controlName: string): string {
    if (this.serverErrors[controlName]) return this.serverErrors[controlName];
    const errors = this.formGroup.get(controlName)?.errors;
    if (!errors) return '';
    if (errors.required) return 'Trường này là bắt buộc.';
    if (errors.pattern) {
      const messages: { [key: string]: string } = {
        MaNV: 'Mã NV phải có dạng NV + chữ số, ví dụ NV105.',
        SDT: 'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.',
        CCCD: 'CCCD gồm 12 số hoặc CMND cũ gồm 9 số.',
        PhongBan: 'Phòng ban phải là mã số dương.',
      };
      return messages[controlName] || 'Dữ liệu không đúng định dạng.';
    }
    if (errors.email) return 'Email không đúng định dạng.';
    if (errors.minlength) return `Cần tối thiểu ${errors.minlength.requiredLength} ký tự.`;
    if (errors.maxlength) return `Không được quá ${errors.maxlength.requiredLength} ký tự.`;
    return 'Dữ liệu không hợp lệ.';
  }

  private showApiMessage(message: string): void {
    const field = message.includes('Mã nhân viên') || message.includes('Mã NV')
      ? 'MaNV'
      : message.includes('CCCD') || message.includes('CMND')
        ? 'CCCD'
        : message.includes('Số điện thoại')
          ? 'SDT'
          : message.includes('Email')
            ? 'Email'
            : message.includes('Phòng ban')
              ? 'PhongBan'
              : null;

    if (field) {
      this.serverErrors[field] = message;
      this.formGroup.get(field)?.markAsTouched();
      return;
    }

    window.alert(message);
  }

  goBack(): void {
    this.dialogRef.close();
  }
}
