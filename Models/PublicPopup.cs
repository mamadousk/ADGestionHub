using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models;

public sealed class PublicPopup
{
    public const int MaxTitleLength = 120;

    public int Id { get; set; }
    [MaxLength(MaxTitleLength)] public string Title { get; set; } = string.Empty;
    public string MessageHtml { get; set; } = string.Empty;
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? WithdrawnAtUtc { get; set; }

    public bool IsVisibleAt(DateTimeOffset now) => WithdrawnAtUtc == null && ExpiresAtUtc > now;
}
