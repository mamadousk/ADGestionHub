using System.Collections.Generic;

namespace AdGestionHub.Models
{
    public class DashboardViewModel
    {
        public decimal TotalSales { get; set; }
        public decimal TotalSubtotalNet { get; set; }
        public decimal TotalTaxCollected { get; set; }
        public decimal TodaySales { get; set; }
        public int TodayTransactions { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalDebts { get; set; }
        public decimal? MonthlyGrowthPercent { get; set; }
        public int TotalTransactions { get; set; }
        public int LowStockCount { get; set; }
        public bool IsVatEnabled { get; set; }
        public bool IsFormalized { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public string CurrencySymbol { get; set; } = "$";
        public int CurrencyDecimals { get; set; } = 2;
        public List<Sale> RecentSales { get; set; } = new List<Sale>();
        public List<Expense> RecentExpenses { get; set; } = new List<Expense>();
        public List<Product> LowStockProducts { get; set; } = new List<Product>();
        public int TotalBoutiques { get; set; }
    }
}
