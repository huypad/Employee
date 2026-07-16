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

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    public dialogRef: MatDialogRef<NhanVienManagementEditDialogComponent>,
    private fb: FormBuilder,
    private service: NhanVienManagementService
  ) {}

  ngOnInit(): void {
    this.formGroup = this.fb.group({
      MaNV: ['', Validators.required],
      HoTen: ['', Validators.required],
      SDT: [''],
      CCCD: [''],
      Email: [''],
      DiaChi: [''],
      PhongBan: [''],
      ChucVu: [''],
    });

    if (this.data?.item) {
      this.formGroup.patchValue(this.data.item);
    }
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

    window.alert(res?.error?.message || 'Không thể lưu nhân viên.');
  }

  private showError(error: any): void {
    window.alert(error?.error?.error?.message || error?.error?.message || 'Không thể kết nối API.');
  }

  goBack(): void {
    this.dialogRef.close();
  }
}
