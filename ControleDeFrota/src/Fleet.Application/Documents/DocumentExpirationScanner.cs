using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Common;
using Fleet.Domain.Documents;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Documents;

/// <summary>
/// Time-based side of the document alerts (ADR-025). Statuses are always computed on read; this job only emits
/// DocumentExpiring / DocumentExpired events once per state change, for the future notification channels.
/// Runs outside a request (no current user), for every company.
/// </summary>
public sealed class DocumentExpirationScanner(IFleetDbContext db, IClock clock)
{
    public const int BatchSize = 500;

    public async Task<int> ScanAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var emitted = 0;
        while (true)
        {
            // No tenant exists in a background job: filters are ignored and re-applied explicitly
            // (not deleted, live company/owner). Documents are only read and flagged; each event carries its own CompanyId.
            var batch = await db.Documents.IgnoreQueryFilters()
                .Include(d => d.DocumentType)
                .Include(d => d.Vehicle)
                .Include(d => d.Driver)
                .Include(d => d.Implement)
                .Where(d => d.DeletedAt == null && d.ReplacedAt == null && d.AlertStartsOn != null && d.AlertStartsOn <= today &&
                            db.Companies.IgnoreQueryFilters().Any(c => c.Id == d.CompanyId && c.DeletedAt == null && c.IsActive))
                .Where(d => d.LastAlertedStatus == null ||
                            (d.ExpiresOn < today ? d.LastAlertedStatus != DocumentStatus.Expired : d.LastAlertedStatus != DocumentStatus.ExpiringSoon))
                .OrderBy(d => d.Id)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) return emitted;

            foreach (var document in batch)
            {
                var status = DocumentExpiryPolicy.Evaluate(document, today);
                document.LastAlertedStatus = status;
                if (!IsOwnerAlive(document)) continue;
                db.OperationalEvents.Add(CreateEvent(document, status, today));
                emitted++;
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private OperationalEvent CreateEvent(Document d, DocumentStatus status, DateOnly today)
    {
        var days = DocumentExpiryPolicy.DaysUntilExpiration(d.ExpiresOn, today)!.Value;
        var what = d.OwnerType switch
        {
            DocumentOwnerType.Vehicle => $"{d.DocumentType.Name} do veículo {LicensePlate.Format(d.Vehicle!.LicensePlate)}",
            DocumentOwnerType.Implement => $"{d.DocumentType.Name} do implemento {LicensePlate.Format(d.Implement!.LicensePlate)}",
            DocumentOwnerType.Driver => $"{d.DocumentType.Name} de {d.Driver!.FullName}",
            _ => $"{d.DocumentType.Name} da empresa",
        };
        var date = BrazilianFormat.Date(d.ExpiresOn!.Value);
        var (type, summary) = status == DocumentStatus.Expired
            ? (OperationalEventType.DocumentExpired, $"{what} venceu em {date}.")
            : (OperationalEventType.DocumentExpiring, days switch
            {
                0 => $"{what} vence hoje ({date}).",
                1 => $"{what} vence amanhã ({date}).",
                _ => $"{what} vence em {days} dias ({date}).",
            });

        return OperationalEventLog.Create(type, DocumentService.Subject(d), summary,
            new { documentTypeId = d.DocumentTypeId, d.OwnerType, expiresOn = d.ExpiresOn, daysUntilExpiration = days },
            clock.UtcNow, userId: null, d.CompanyId);
    }

    private static bool IsOwnerAlive(Document d) => d.OwnerType switch
    {
        DocumentOwnerType.Vehicle => d.Vehicle is { DeletedAt: null },
        DocumentOwnerType.Driver => d.Driver is { DeletedAt: null },
        DocumentOwnerType.Implement => d.Implement is { DeletedAt: null },
        _ => true,
    };
}
