import { Component, OnInit ,ViewChild ,ElementRef} from '@angular/core';
import { ShopService } from './shop-service';
import { IPagnation } from '../shared/Models/Pagnation';
import { IProduct } from '../shared/Models/Product';
import { ICategory } from '../shared/Models/Category';
import { ProductParam } from '../shared/Models/ProductParam';
import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-shop',
  standalone: false,
  templateUrl: './shop.html',
  styleUrl: './shop.scss'
})
export class Shop implements OnInit{
  constructor(private shopService:ShopService,private toast:ToastrService){}

  ngOnInit(): void {
    this.ProductParam.SortSelected = this.SortingOption[0].value;
    this.getAllProduct();
    this.getCategory();
  }

  product: IProduct[];
  Cateogry: ICategory[];
  TotatlCount: number;
  ProductParam = new ProductParam();

  getAllProduct() {
    this.shopService.getProduct(this.ProductParam).subscribe({
      next: (value: IPagnation) => {
        this.product = value.data;
        this.TotatlCount = value.totalCount;
        this.ProductParam.pageNumber = value.pageNumber;
        this.ProductParam.pageSize = value.pageSize;
        console.log(this.product);
        this.toast.success('Product Loaded Successfully', 'SUCCESS');
      },
    });
  }

  OnChangePage(event: any) {
    if (this.ProductParam.pageNumber != event) {
      this.ProductParam.pageNumber = event;
      console.log(event);

      this.getAllProduct();
    }
  }
  getCategory() {
    this.shopService.getCategory().subscribe({
      next: (value) => {
        this.Cateogry = value;
        console.log(this.Cateogry);
      },
    });
  }

  SelectedId(categoryid: number) {
    this.ProductParam.CategoryId = categoryid;
    this.ProductParam.pageNumber = 1;
    this.getAllProduct();
  }

  SortingOption = [
    { name: 'Price', value: 'Name' },
    { name: 'Price:min-max', value: 'PriceAce' },
    { name: 'Price:max-min', value: 'PriceDce' },
  ];

  SortingByPrice(sort: Event) {
    this.ProductParam.SortSelected = (sort.target as HTMLSelectElement).value;
    this.getAllProduct();
  }

  OnSearch(Search: string) {
    this.ProductParam.search = Search;
    this.getAllProduct();
  }
  @ViewChild('search') searchInput: ElementRef; // Reference to the search input element
  @ViewChild('SortSelected') selected: ElementRef; // Reference to the sorting select element

  // Reset all values
  ResetValue() {
    this.ProductParam.search = '';
    this.ProductParam.SortSelected = this.SortingOption[0].value;
    this.ProductParam.CategoryId = 0;

    this.searchInput.nativeElement.value = '';

    this.selected.nativeElement.selectedIndex = 0;

    this.getAllProduct();
  }
}
