namespace Fleet.Application.Common;

/// <summary>
/// Application errors. Messages are shown to the end user: pt-BR, saying what happened and how to fix it.
/// Mapped to HTTP status codes by the API exception handler.
/// </summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>404 — also used for records of another company, to avoid revealing they exist.</summary>
public sealed class NotFoundException(string message) : AppException(message);

/// <summary>409 — duplicate plate, CPF, CNPJ, e-mail…</summary>
public sealed class ConflictException(string message, string? field = null) : AppException(message)
{
    /// <summary>Request field that caused the conflict (camelCase), so the UI can highlight it.</summary>
    public string? Field { get; } = field;
}

/// <summary>422 — a business rule forbids the operation.</summary>
public sealed class BusinessRuleException(string message) : AppException(message);

/// <summary>403 — allowed by the base permission, but not for this target (e.g. privilege escalation).</summary>
public sealed class ForbiddenException(string message) : AppException(message);

/// <summary>401 — invalid credentials, locked/inactive account, expired session.</summary>
public sealed class AuthenticationFailedException(string message) : AppException(message);
