using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

public sealed record OperationAuthor(string? Name, string? Identifier)
{
    private string Label => string.IsNullOrWhiteSpace(Name) ? Identifier ?? "Auteur non renseigné" : Name;
    public string Caption => string.IsNullOrWhiteSpace(Identifier) || Identifier == Label ? Label : $"{Label} · {Identifier}";
}

public sealed class OperationJournal(ApplicationDbContext db)
{
    public const int PageSize = 25;

    private IQueryable<SystemLog> Audit(int shop) => db.SystemLogs.AsNoTracking()
        .Where(l => l.BoutiqueId == shop && l.ErrorMessage.StartsWith(SystemLog.AuditPrefix));

    public async Task<IReadOnlyDictionary<string, OperationAuthor>> AuthorsAsync(int shop, IEnumerable<string?> userIds, CancellationToken ct)
    {
        var authors = new Dictionary<string, OperationAuthor>();
        foreach (var ids in userIds.OfType<string>().Where(id => id.Length != 0).Distinct().Chunk(500))
        {
            var members = await db.Users.AsNoTracking().Where(u => u.BoutiqueId == shop && ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, Identifier = u.UserName ?? u.Email }).ToListAsync(ct);
            foreach (var member in members) authors.Add(member.Id, new(member.FullName, member.Identifier));

            var missing = ids.Where(id => !authors.ContainsKey(id)).ToArray();
            if (missing.Length == 0) continue;
            var journal = Audit(shop).Where(l => l.ActorUserId != null && missing.Contains(l.ActorUserId));
            var snapshots = journal.GroupBy(l => l.ActorUserId).Select(g => g.Max(l => l.Id));
            var former = await journal.Where(l => snapshots.Contains(l.Id))
                .Select(l => new { l.ActorUserId, l.ActorName, l.UserEmail }).ToListAsync(ct);
            foreach (var author in former) authors.Add(author.ActorUserId!, new(author.ActorName, author.UserEmail));
        }
        return authors;
    }

    public async Task<IReadOnlyDictionary<int, OperationAuthor>> ClientAuthorsAsync(int shop, IEnumerable<int> clientIds, CancellationToken ct)
    {
        var authors = new Dictionary<int, OperationAuthor>();
        foreach (var ids in clientIds.Distinct().Chunk(500))
        {
            var changes = Audit(shop).Where(l => l.Subject == OperationSubject.Client && l.SubjectId.HasValue
                && ids.Contains(l.SubjectId.Value) && l.ActorUserId != null);
            var latest = changes.GroupBy(l => l.SubjectId).Select(g => g.Max(l => l.Id));
            var rows = await changes.Where(l => latest.Contains(l.Id))
                .Select(l => new { l.SubjectId, l.ActorName, l.UserEmail }).ToListAsync(ct);
            foreach (var row in rows) authors.Add(row.SubjectId!.Value, new(row.ActorName, row.UserEmail));
        }
        return authors;
    }

    public async Task<(IReadOnlyList<StaffActivityEntry> Entries, bool HasNext)> HistoryAsync(int shop, string employeeId,
        DateTime? from, DateTime? until, int pageNumber, CancellationToken ct)
    {
        var journal = Audit(shop).Where(l => l.ActorUserId == employeeId);
        // Recover only older documents with an explicit author ID. Never match an old audit by
        // username/email (which can be reused), nor credit a later payment to the original seller.
        var olderSales = db.Sales.AsNoTracking().Where(s => s.BoutiqueId == shop && s.CreatedByUserId == employeeId
            && !journal.Any(l => l.Subject == OperationSubject.Sale && l.SubjectId == s.Id));
        var olderExpenses = db.Expenses.AsNoTracking().Where(e => e.BoutiqueId == shop && e.UserId == employeeId
            && !journal.Any(l => l.Subject == OperationSubject.Expense && l.SubjectId == e.Id));

        // Page the common keys first, so neither old documents nor journal descriptions are loaded without a bound.
        var query = journal.Select(l => new { Source = 0, l.Id, Date = l.DateOccurred })
            .Concat(olderSales.Select(s => new { Source = 1, s.Id, Date = s.SaleDate }))
            .Concat(olderExpenses.Select(e => new { Source = 2, e.Id, e.Date }));
        if (from.HasValue) query = query.Where(row => row.Date >= from.Value);
        if (until.HasValue) query = query.Where(row => row.Date < until.Value);
        var keys = await query.OrderByDescending(row => row.Date).ThenByDescending(row => row.Source).ThenByDescending(row => row.Id)
            .Skip((pageNumber - 1) * PageSize).Take(PageSize + 1).ToListAsync(ct);
        var page = keys.Take(PageSize).ToArray();
        var entries = new Dictionary<(int Source, int Id), StaffActivityEntry>();
        var logIds = page.Where(row => row.Source == 0).Select(row => row.Id).ToArray();
        if (logIds.Length != 0)
        {
            var logs = await journal.Where(l => logIds.Contains(l.Id)).ToListAsync(ct);
            foreach (var log in logs) entries[(0, log.Id)] = new(log.DateOccurred,
                log.ErrorMessage[SystemLog.AuditPrefix.Length..], log.StackTrace, log.Subject == OperationSubject.Sale ? log.SubjectId : null);
        }
        var saleIds = page.Where(row => row.Source == 1).Select(row => row.Id).ToArray();
        if (saleIds.Length != 0)
        {
            var sales = await olderSales.Where(s => saleIds.Contains(s.Id)).ToListAsync(ct);
            foreach (var sale in sales) entries[(1, sale.Id)] = new(sale.SaleDate, $"Vente {sale.Number} enregistrée",
                $"{Money.Display(sale.FinalPrice, sale.CurrencyCode)}{(sale.IsCancelled ? " · Annulée" : "")}", sale.Id);
        }
        var expenseIds = page.Where(row => row.Source == 2).Select(row => row.Id).ToArray();
        if (expenseIds.Length != 0)
        {
            var expenses = await olderExpenses.Where(e => expenseIds.Contains(e.Id)).ToListAsync(ct);
            foreach (var expense in expenses) entries[(2, expense.Id)] = new(expense.Date, "Dépense enregistrée",
                $"{expense.Description} · {Money.Display(expense.Amount, expense.CurrencyCode)} · Date de la dépense (historique)", null);
        }
        // For example, an administrator may remove an old expense between selecting and resolving the keys.
        return (page.Select(row => entries.GetValueOrDefault((row.Source, row.Id))).OfType<StaffActivityEntry>().ToList(), keys.Count > PageSize);
    }
}
