using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RideMatching.Domain.Exceptions;

namespace RideMatching.Api.Middleware;

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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, detail) = exception switch
        {
            ConcurrencyConflictException cex => (HttpStatusCode.Conflict, "Concurrency Conflict", cex.Message),
            InvalidStateTransitionException sex => (HttpStatusCode.UnprocessableEntity, "Invalid State Transition", sex.Message),
            RideDomainException dex => (HttpStatusCode.BadRequest, "Domain Rule Violation", dex.Message),
            KeyNotFoundException knf => (HttpStatusCode.NotFound, "Resource Not Found", knf.Message),
            ArgumentException aex => (HttpStatusCode.BadRequest, "Invalid Argument", aex.Message),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error", $"{exception.Message} ({exception.GetType().Name})")
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
