import { Injectable } from '@angular/core';
import { HttpClient,HttpParams } from '@angular/common/http';
import { IPagnation } from '../shared/Models/Pagnation';
import { ICategory } from '../shared/Models/Category';
import { ProductParam } from '../shared/Models/ProductParam';
import { IProduct } from '../shared/Models/Product';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ShopService {

  constructor(private http:HttpClient) { }
 
  getProduct(productParam:ProductParam) {
    let param=new HttpParams();
    if(productParam.CategoryId){
      param=param.append("categoryId",productParam.CategoryId)
    }
    if(productParam.SortSelected){
      param=param.append("Sort",productParam.SortSelected)
    }
    if(productParam.search){
      param=param.append("Search",productParam.search)
    }
    
    param=param.append("pageNumber",productParam.pageNumber)
    param=param.append("pageSize",productParam.pageSize)
    
    return this.http.get<IPagnation>(environment.baseURL+"Products/get-all",{params:param});
  }
  // Get Category
  getCategory(){
    return this.http.get<ICategory[]>(environment.baseURL+"Categories/get-all")
  }
  getProductDetails(id:number){
    return this.http.get<IProduct>(environment.baseURL+"Products/get-by-id/"+id)
  }
  // getProductRating(id:number){
  //   return this.http.get<IReview[]>(this.baseURL+"Ratings/get-rating/"+id)
  // }
}
