namespace Fleet.Domain.Documents;

public enum DocumentStatus
{
    Valid,
    ExpiringSoon,
    Expired,
    /// <summary>The document type does not expire (or the document has no expiration date).</summary>
    NoExpiration,
    /// <summary>Replaced by a renewal — history only, never alerts.</summary>
    Replaced,
}

/// <summary>
/// The ONLY place that turns dates into a document status (ADR-021). The rules, with the alert threshold configured per
/// document type (<see cref="DocumentType.AlertDaysBefore"/>):
/// <list type="bullet">
/// <item>Expired: ExpiresOn &lt; today.</item>
/// <item>ExpiringSoon: today ≤ ExpiresOn ≤ today + AlertDaysBefore.</item>
/// <item>Valid: ExpiresOn &gt; today + AlertDaysBefore.</item>
/// <item>NoExpiration: the type does not expire or no date was given.</item>
/// </list>
/// The first day of the alert window is stored on the document (<see cref="Document.AlertStartsOn"/>), so database
/// queries apply the same rules comparing plain dates (DocumentQueries in the Application layer).
/// </summary>
public static class DocumentExpiryPolicy
{
    /// <summary>Null when the document does not expire.</summary>
    public static DateOnly? AlertStartsOn(DateOnly? expiresOn, bool typeHasExpiration, int alertDaysBefore) =>
        typeHasExpiration && expiresOn is { } date ? date.AddDays(-alertDaysBefore) : null;

    public static DocumentStatus Evaluate(DateOnly? expiresOn, DateOnly? alertStartsOn, DateOnly today, bool replaced)
    {
        if (replaced) return DocumentStatus.Replaced;
        if (expiresOn is not { } date || alertStartsOn is not { } alertFrom) return DocumentStatus.NoExpiration;
        if (date < today) return DocumentStatus.Expired;
        return alertFrom <= today ? DocumentStatus.ExpiringSoon : DocumentStatus.Valid;
    }

    public static DocumentStatus Evaluate(DateOnly? expiresOn, int alertDaysBefore, DateOnly today, bool replaced = false) =>
        Evaluate(expiresOn, AlertStartsOn(expiresOn, typeHasExpiration: true, alertDaysBefore), today, replaced);

    public static DocumentStatus Evaluate(Document document, DateOnly today) =>
        Evaluate(document.ExpiresOn, document.AlertStartsOn, today, document.ReplacedAt is not null);

    /// <summary>Days until expiration (negative = days since it expired); null when it does not expire.</summary>
    public static int? DaysUntilExpiration(DateOnly? expiresOn, DateOnly today) =>
        expiresOn is { } date ? date.DayNumber - today.DayNumber : null;

    /// <summary>Only these statuses produce alerts and dashboard counters.</summary>
    public static bool IsAlert(DocumentStatus status) => status is DocumentStatus.ExpiringSoon or DocumentStatus.Expired;
}
