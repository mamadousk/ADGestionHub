using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdGestionHub.Models;

public sealed class TermsAcceptance
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    [MaxLength(32)] public string Version { get; set; } = "";
    public DateTimeOffset AcceptedAtUtc { get; set; }
}

// A separate row keeps image data out of every ordinary settings/tenant query.
public sealed class StoreLogo
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int BoutiqueId { get; set; }
    public byte[] Png { get; set; } = [];
}
