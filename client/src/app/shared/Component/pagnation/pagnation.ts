import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-pagnation',
  standalone: false,
  templateUrl: './pagnation.html',
  styleUrl: './pagnation.scss',
})
export class Pagnation {
  @Input() pageSize: number;

  @Input() totalCount: number;

  @Output() pageChanged = new EventEmitter();

  OnChangePage(ev: any) {
    this.pageChanged.emit(ev);
  }
}
