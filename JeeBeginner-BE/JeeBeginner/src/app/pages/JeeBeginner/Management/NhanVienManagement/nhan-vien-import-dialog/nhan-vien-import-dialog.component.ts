import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { NhanVienManagementService } from '../Services/nhan-vien-management.service';

@Component({
  selector: 'app-nhan-vien-import-dialog',
  templateUrl: './nhan-vien-import-dialog.component.html',
})
export class NhanVienImportDialogComponent {
  selectedFile?: File;
  isImporting = false;
  result: { success: number; failed: number; errors: Array<{ row: number; message: string }> } | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    public dialogRef: MatDialogRef<NhanVienImportDialogComponent>,
    private service: NhanVienManagementService
  ) {}

  selectFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files && input.files.length ? input.files[0] : undefined;
    this.result = null;
  }

  importFile(): void {
    if (!this.selectedFile) {
      window.alert('Vui lòng chọn file Excel .xlsx.');
      return;
    }
    this.isImporting = true;
    this.service.importNhanVienFromExcel(this.selectedFile).subscribe(
      (res) => {
        this.isImporting = false;
        if (res?.status === 1) {
          this.result = res.data;
          return;
        }
        window.alert(res?.error?.message || 'Không thể import file.');
      },
      (error) => {
        this.isImporting = false;
        window.alert(error?.error?.error?.message || error?.error?.message || 'File import không hợp lệ.');
      }
    );
  }

  downloadTemplate(): void {
    this.service.downloadNhanVienImportTemplate().subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'Mau_import_nhan_vien.xlsx';
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  goBack(): void {
    this.dialogRef.close(this.result?.success ? this.result : null);
  }
}
