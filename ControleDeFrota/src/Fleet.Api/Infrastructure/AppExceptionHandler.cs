using System.Diagnostics;
using Fleet.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Api.Infrastructure;

/// <summary>
/// Single translation point from exceptions to RFC 9457 ProblemDetails. Titles are user-facing pt-BR messages;
/// unexpected errors never leak details — the traceId lets support find the log entry.
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    private const string GenericError =
        "Não foi possível concluir a operação. Tente novamente em instantes. Se o problema persistir, informe o código de rastreio ao suporte.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException ex => ValidationProblem(ex),
            NotFoundException ex => Problem(StatusCodes.Status404NotFound, ex.Message),
            ConflictException ex => ConflictProblem(ex),
            BusinessRuleException ex => Problem(StatusCodes.Status422UnprocessableEntity, ex.Message),
            ForbiddenException ex => Problem(StatusCodes.Status403Forbidden, ex.Message),
            AuthenticationFailedException ex => Problem(StatusCodes.Status401Unauthorized, ex.Message),
            // Unique index hit by a concurrent request that passed the service-level duplicate check.
            DbUpdateException { InnerException: not null } ex when IsUniqueViolation(ex) =>
                Problem(StatusCodes.Status409Conflict, "Já existe um registro com estes dados. Atualize a página e confira as informações."),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, GenericError);
        }

        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title) => new() { Status = status, Title = title };

    private static ProblemDetails ConflictProblem(ConflictException ex)
    {
        var problem = Problem(StatusCodes.Status409Conflict, ex.Message);
        if (ex.Field is not null) problem.Extensions["errors"] = new Dictionary<string, string[]> { [ex.Field] = [ex.Message] };
        return problem;
    }

    private static ProblemDetails ValidationProblem(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => ToCamelCasePath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        var problem = Problem(StatusCodes.Status400BadRequest, "Alguns campos precisam de atenção. Corrija os itens destacados e tente novamente.");
        problem.Extensions["errors"] = errors;
        return problem;
    }

    /// <summary>"Address.ZipCode" → "address.zipCode", matching the JSON the client sent.</summary>
    private static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException!.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
        ex.InnerException.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
}
