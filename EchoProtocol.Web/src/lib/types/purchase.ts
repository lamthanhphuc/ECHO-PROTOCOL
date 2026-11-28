export interface PurchaseHistoryItem {
  purchaseId: string;
  itemId: string;
  itemName: string;
  category: string;
  pricePaid: number;
  status: string;
  purchasedAtUtc: string;
}

export interface PurchaseHistoryPage {
  items: PurchaseHistoryItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}
