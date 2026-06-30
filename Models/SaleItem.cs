namespace AdGestionHub.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public virtual Sale? Sale { get; set; }

        public int? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int BoutiqueId { get; set; }
   
    }
}