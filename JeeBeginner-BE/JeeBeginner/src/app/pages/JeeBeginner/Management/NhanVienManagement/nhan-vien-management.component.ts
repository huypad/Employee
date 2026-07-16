import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';

@Component({
  selector: 'app-nhan-vien-management',
  templateUrl: './nhan-vien-management.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NhanVienManagementComponent implements OnInit {
  constructor() {}

  ngOnInit(): void {}
}
