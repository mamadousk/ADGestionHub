namespace AdGestionHub.Models.ViewModels;

public sealed record StaffActivityEntry(DateTime Date, string Title, string? Details, int? SaleId);

public sealed record StaffActivityPage(UserViewModel Employee, DateTime? From, DateTime? To, int PageNumber, bool HasNext,
    IReadOnlyList<StaffActivityEntry> Entries);
