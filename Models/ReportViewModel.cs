using System;
using System.Collections.Generic;

namespace AdGestionHub.Models
{
    public class ReportViewModel
    {
        public string StoreName { get; set; } = "Ma Boutique";
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today;
        public List<Sale> Sales { get; set; } = new List<Sale>();
        public List<Expense> Expenses { get; set; } = new List<Expense>();
        public decimal TotalSales { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => GrossProfit - TotalExpenses;
        public decimal CashFlowNet => TotalSales - TotalExpenses;
        public Dictionary<string, decimal> SalesByPaymentMethod { get; set; } = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new Dictionary<string, decimal>();
    }
}
