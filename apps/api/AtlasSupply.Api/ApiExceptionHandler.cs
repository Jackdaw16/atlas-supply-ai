using Microsoft.AspNetCore.Diagnostics;

namespace AtlasSupply.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var result = exception switch
        {
            ArgumentException argumentException => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = [argumentException.Message]
                },
                title: "Request validation failed."),
            BadHttpRequestException badHttpRequestException => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = [badHttpRequestException.Message]
                },
                title: "Request validation failed."),
            InvalidOperationException invalidOperationException => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Request cannot be processed.",
                detail: invalidOperationException.Message),
            _ => HandleUnexpectedException(exception)
        };

        await result.ExecuteAsync(httpContext);
        return true;
    }

    private IResult HandleUnexpectedException(Exception exception)
    {
        logger.LogError(exception, "An unhandled exception occurred while processing the request.");

        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.");
    }
}
