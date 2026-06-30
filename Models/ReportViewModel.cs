using System;
using System.Collections.Generic;

namespace AdGestionHub.Models
{
    public class ReportViewModel
    {
        public List<Sale> Sales { get; set; } = new List<Sale>();
        public List<Expense> Expenses { get; set; } = new List<Expense>();
        public decimal TotalSales { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalSales - TotalExpenses;
    }
}