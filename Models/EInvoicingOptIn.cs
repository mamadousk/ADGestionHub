using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class EInvoicingOptIn
    {
        public int Id { get; set; }

        public int BoutiqueId { get; set; }

        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [StringLength(10)]
        public string CountryCode { get; set; } = "CD";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
