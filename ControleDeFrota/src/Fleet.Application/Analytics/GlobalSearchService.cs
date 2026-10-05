using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Documents;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Validation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Analytics;

public enum SearchResultType
{
    Vehicle,
    Implement,
    Driver,
    Tire,
    WorkOrder,
    Fueling,
    Expense,
    Document,
    Occurrence,
    Alert,
}

public sealed record SearchResult(SearchResultType Type, string Title, string? Subtitle, string Link);

public sealed record SearchResponse(string Query, IReadOnlyList<SearchResult> Results);

/// <summary>
/// One search box for the whole product (spec §23). Each record type is searched only when the reader can see that
/// module (same permission as its list); at most <see cref="PerType"/> hits per type, newest/most relevant first.
/// </summary>
public sealed class GlobalSearchService(IFleetDbContext db, IClock clock, ICurrentUser currentUser)
{
    public const int PerType = 5;
    public const int MinLength = 2;
    public const int MaxLength = 60;

    private bool Can(string permission) => currentUser.HasPermission(permission);

    public async Task<SearchResponse> SearchAsync(string? query, CancellationToken ct)
    {
        var term = query?.Trim() ?? "";
        if (term.Length < MinLength) return new SearchResponse(term, []);
        if (term.Length > MaxLength) term = term[..MaxLength];
        var plate = LicensePlate.Normalize(term);
        var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
        var results = new List<SearchResult>();

        if (Can(Permissions.Vehicles.View))
        {
            var vehicles = await db.Vehicles
                .Where(v => (plate.Length >= 2 && v.LicensePlate.Contains(plate)) || v.Model.Contains(term) || v.Chassis.Contains(plate))
                .OrderBy(v => v.LicensePlate).Take(PerType)
                .Select(v => new { v.Id, v.LicensePlate, v.Manufacturer, v.Model }).ToListAsync(ct);
            results.AddRange(vehicles.Select(v => new SearchResult(SearchResultType.Vehicle, LicensePlate.Format(v.LicensePlate),
                $"{v.Manufacturer} {v.Model}", $"/veiculos/{v.Id}")));
        }
        if (Can(Permissions.Implements.View))
        {
            var implements = await db.Implements
                .Where(i => (plate.Length >= 2 && i.LicensePlate.Contains(plate)) || i.Model.Contains(term))
                .OrderBy(i => i.LicensePlate).Take(PerType)
                .Select(i => new { i.Id, i.LicensePlate, i.Manufacturer, i.Model }).ToListAsync(ct);
            results.AddRange(implements.Select(i => new SearchResult(SearchResultType.Implement, LicensePlate.Format(i.LicensePlate),
                $"{i.Manufacturer} {i.Model}", $"/implementos/{i.Id}")));
        }
        if (Can(Permissions.Drivers.View))
        {
            var drivers = await db.Drivers
                .Where(d => d.FullName.Contains(term) || (digits.Length >= 3 && d.Cpf.Contains(digits)))
                .OrderBy(d => d.FullName).Take(PerType)
                .Select(d => new { d.Id, d.FullName, d.Status }).ToListAsync(ct);
            results.AddRange(drivers.Select(d => new SearchResult(SearchResultType.Driver, d.FullName, null, $"/motoristas/{d.Id}")));
        }
        if (Can(Permissions.Tires.View))
        {
            var tires = await db.Tires.Where(t => t.Code.Contains(plate))
                .OrderBy(t => t.Code).Take(PerType).Select(t => new { t.Id, t.Code, t.Status }).ToListAsync(ct);
            results.AddRange(tires.Select(t => new SearchResult(SearchResultType.Tire, t.Code, null, $"/pneus/{t.Id}")));
        }
        if (Can(Permissions.Maintenance.View))
        {
            var number = int.TryParse(digits, out var n) && digits.Length <= 9 ? n : (int?)null;
            var orders = await db.WorkOrders
                .Where(w => (number != null && w.Sequence == number) || w.Description.Contains(term) ||
                            (plate.Length >= 3 && w.Vehicle.LicensePlate.Contains(plate)))
                .OrderByDescending(w => w.OpenedAt).Take(PerType)
                .Select(w => new { w.Id, w.Sequence, w.Description, w.Vehicle.LicensePlate, w.OpenedAt }).ToListAsync(ct);
            results.AddRange(orders.Select(w => new SearchResult(SearchResultType.WorkOrder, $"OS-{w.Sequence:D6} · {LicensePlate.Format(w.LicensePlate)}",
                Short(w.Description), $"/ordens-servico/{w.Id}")));
        }
        if (Can(Permissions.Fuel.View))
        {
            var fuelings = await db.Fuelings
                .Where(f => (f.ReceiptNumber != null && f.ReceiptNumber.Contains(term)) || (plate.Length >= 3 && f.Vehicle.LicensePlate.Contains(plate)))
                .OrderByDescending(f => f.FueledAt).Take(PerType)
                .Select(f => new { f.Id, f.Vehicle.LicensePlate, f.FueledAt, f.Quantity, Fuel = f.FuelType.Name }).ToListAsync(ct);
            results.AddRange(fuelings.Select(f => new SearchResult(SearchResultType.Fueling,
                $"Abastecimento · {LicensePlate.Format(f.LicensePlate)}", $"{clock.FormatDateTime(f.FueledAt)} · {BrazilianFormat.Number(f.Quantity, 1)} {f.Fuel}",
                $"/abastecimentos/{f.Id}")));
        }
        if (Can(Permissions.Finance.View))
        {
            var expenses = await db.Expenses
                .Where(e => e.Description.Contains(term) || (e.ReferenceNumber != null && e.ReferenceNumber.Contains(term)) ||
                            (plate.Length >= 3 && e.Vehicle != null && e.Vehicle.LicensePlate.Contains(plate)))
                .OrderByDescending(e => e.ExpenseDate).Take(PerType)
                .Select(e => new { e.Id, e.Description, e.ExpenseDate, Category = e.ExpenseCategory.Name }).ToListAsync(ct);
            // No R$ here: search results are shown to finance.view, the amount needs finance.viewcosts.
            results.AddRange(expenses.Select(e => new SearchResult(SearchResultType.Expense, e.Description,
                $"{e.Category} · {BrazilianFormat.Date(e.ExpenseDate)}", $"/financeiro/despesas/{e.Id}/editar")));
        }
        if (Can(Permissions.Documents.View))
        {
            var canSeeDrivers = Can(Permissions.Drivers.View);
            var documents = await db.Documents
                .Where(d => d.ReplacedByDocumentId == null &&
                            ((d.Number != null && d.Number.Contains(term)) || d.DocumentType.Name.Contains(term) ||
                             (plate.Length >= 3 && d.Vehicle != null && d.Vehicle.LicensePlate.Contains(plate))) &&
                            (canSeeDrivers || d.OwnerType != DocumentOwnerType.Driver))
                .OrderBy(d => d.ExpiresOn).Take(PerType)
                .Select(d => new
                {
                    d.Id, d.OwnerType, d.VehicleId, d.DriverId, d.ImplementId, TypeName = d.DocumentType.Name, d.ExpiresOn,
                    Owner = d.Vehicle != null ? d.Vehicle.LicensePlate : d.Implement != null ? d.Implement.LicensePlate : d.Driver != null ? d.Driver.FullName : null,
                })
                .ToListAsync(ct);
            results.AddRange(documents.Select(d => new SearchResult(SearchResultType.Document,
                $"{d.TypeName}{(d.Owner is null ? "" : " · " + (d.OwnerType == DocumentOwnerType.Driver ? d.Owner : LicensePlate.Format(d.Owner)))}",
                d.ExpiresOn is { } e ? $"vence em {BrazilianFormat.Date(e)}" : null,
                d.OwnerType switch
                {
                    DocumentOwnerType.Vehicle => $"/veiculos/{d.VehicleId}?aba=documentos",
                    DocumentOwnerType.Driver => $"/motoristas/{d.DriverId}?aba=documentos",
                    DocumentOwnerType.Implement => $"/implementos/{d.ImplementId}",
                    _ => "/documentos",
                })));
        }
        if (Can(Permissions.Occurrences.View))
        {
            var occurrences = await db.Occurrences
                .Where(o => o.Description.Contains(term) || (plate.Length >= 3 && o.Vehicle != null && o.Vehicle.LicensePlate.Contains(plate)))
                .OrderByDescending(o => o.OccurredAt).Take(PerType)
                .Select(o => new { o.Id, o.Description, o.OccurredAt }).ToListAsync(ct);
            results.AddRange(occurrences.Select(o => new SearchResult(SearchResultType.Occurrence, Short(o.Description),
                clock.FormatDateTime(o.OccurredAt), $"/ocorrencias/{o.Id}")));
        }
        var audiences = AlertAudiences.VisibleTo(currentUser.Permissions);
        if (audiences.Count > 0)
        {
            var alerts = await db.FleetAlerts
                .Where(a => audiences.Contains(a.Audience) && FleetAlertWorkflow.OpenStatuses.Contains(a.Status) &&
                            (a.Title.Contains(term) || (plate.Length >= 3 && db.Vehicles.Any(v => v.Id == a.VehicleId && v.LicensePlate.Contains(plate)))))
                .OrderByDescending(a => a.Priority).Take(PerType)
                .Select(a => new { a.Id, a.Title }).ToListAsync(ct);
            results.AddRange(alerts.Select(a => new SearchResult(SearchResultType.Alert, a.Title, "Alerta em aberto", $"/alertas/{a.Id}")));
        }
        return new SearchResponse(term, results);
    }

    private static string Short(string text) => text.Length <= 80 ? text : text[..79] + "…";
}
