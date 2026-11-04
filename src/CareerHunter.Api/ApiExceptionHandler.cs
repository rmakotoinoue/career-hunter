using CareerHunter.Application.Assessment;
using CareerHunter.Application.Common;
using CareerHunter.Domain.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CareerHunter.Api;

/// <summary>
/// Maps application and domain failures to RFC 7807 responses. Unexpected exceptions are left
/// to the framework so no stack trace or database detail ever reaches the client.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        switch (exception)
        {
            case BadHttpRequestException badRequest:
                // Malformed JSON, unreadable parameters or a body that exceeds the limit are
                // client errors. Without this case the framework reports them as 500.
                logger.LogWarning(badRequest, "Rejected a request that could not be read.");
                await WriteAsync(
                    httpContext,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Malformed request",
                        Detail = "The request body or parameters could not be read."
                    },
                    cancellationToken);
                return true;

            case DomainValidationException validation:
                await WriteAsync(
                    httpContext,
                    new HttpValidationProblemDetails(
                        validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Validation failed",
                        Detail = "One or more fields are invalid."
                    },
                    cancellationToken);
                return true;

            case NotFoundException notFound:
                await WriteAsync(
                    httpContext,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Resource not found",
                        Detail = notFound.Message
                    },
                    cancellationToken);
                return true;

            case ConflictException conflict:
                await WriteAsync(
                    httpContext,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Conflict",
                        Detail = conflict.Message
                    },
                    cancellationToken);
                return true;

            case AiEvaluationValidationException aiFailure:
                logger.LogWarning(aiFailure, "Rejected AI evaluation response.");
                await WriteAsync(
                    httpContext,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status422UnprocessableEntity,
                        Title = "Invalid evaluation output",
                        Detail = "The AI response did not satisfy the evaluation contract; retry with the rule engine."
                    },
                    cancellationToken);
                return true;

            case BusinessRuleException rule:
                await WriteAsync(
                    httpContext,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Business rule violation",
                        Detail = rule.Message
                    },
                    cancellationToken);
                return true;

            case DomainException domain:
                await WriteAsync(
                    httpContext,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Business rule violation",
                        Detail = domain.Message
                    },
                    cancellationToken);
                return true;

            default:
                return false;
        }
    }

    private static Task WriteAsync(HttpContext httpContext, ProblemDetails problem, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
