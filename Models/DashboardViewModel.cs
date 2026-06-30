namespace AdGestionHub.Models
{
    public class DashboardViewModel
    {
        public decimal TotalSales { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalProfit { get; set; }
        public int TotalTransactions { get; set; }
        public int LowStockCount { get; set; }
        public List<Sale> RecentSales { get; set; } = new List<Sale>();
        public List<Expense> RecentExpenses { get; set; } = new List<Expense>();
        public int TotalBoutiques { get; set; }
    }
}