export interface NhanVienModelDTO {
  Id: number;
  MaNV: string;
  HoTen: string;
  SDT: string;
  CCCD: string;
  Email: string;
  DiaChi: string;
  PhongBan: string;
  ChucVu: string;
  Status: number;
  CreatedDate: string;
}
export class NhanVienModel {
  Id!: number;
  MaNV!: string;
  HoTen!: string;
  SDT!: string;
  CCCD!: string;
  Email!: string;
  DiaChi!: string;
  PhongBan!: string;
  ChucVu!: string;
  Status!: number;
  CreatedDate!: string;

  empty() {
    this.Id = 0;
    this.MaNV = '';
    this.HoTen = '';
    this.SDT = '';
    this.CCCD = '';
    this.Email = '';
    this.DiaChi = '';
    this.PhongBan = '';
    this.ChucVu = '';
    this.Status = 1;
    this.CreatedDate = '';
  }
}
