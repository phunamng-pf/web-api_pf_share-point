namespace SharePoint.Api.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception for request {Method} {Path}", context.Request.Method, context.Request.Path);

            var (statusCode, message) = ex switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request."),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden."),
                FileNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected error.")
            };

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new { message });
        }
    }
}
