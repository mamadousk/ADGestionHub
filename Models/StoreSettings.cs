namespace AdGestionHub.Models
{
    public class StoreSettings
    {
        public int Id { get; set; }
    public int BoutiqueId { get; set; }

    public string? StoreName { get; set; } // Le '?' permet d'éviter les erreurs si c'est vide
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? ReceiptFooterMessage { get; set; }
}
}