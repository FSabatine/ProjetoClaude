using System.Net;
using FluentValidation;
using REC4.Application.Exceptions;

namespace REC4.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, title, errors) = Map(ex);

            if (statusCode == HttpStatusCode.InternalServerError)
                _logger.LogError(ex, "Erro não tratado ao processar {Path}", context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsJsonAsync(new
            {
                title,
                status = (int)statusCode,
                errors
            });
        }
    }

    private static (HttpStatusCode StatusCode, string Title, object? Errors) Map(Exception ex) => ex switch
    {
        NotFoundException => (HttpStatusCode.NotFound, ex.Message, null),
        DuplicateDocumentException => (HttpStatusCode.Conflict, ex.Message, null),
        DuplicateLoginException => (HttpStatusCode.Conflict, ex.Message, null),
        DuplicateGroupNameException => (HttpStatusCode.Conflict, ex.Message, null),
        InvalidCredentialsException => (HttpStatusCode.Unauthorized, ex.Message, null),
        ValidationException validationEx => (HttpStatusCode.BadRequest, "Um ou mais campos são inválidos.",
            validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })),
        _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro inesperado.", null)
    };
}
