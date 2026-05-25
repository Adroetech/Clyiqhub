using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace api.middleware;

public sealed class ServiceApiKeyMiddleware : IMiddleware
{
    private readonly IConfiguration _cfg;

    public ServiceApiKeyMiddleware(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Only protect verify endpoint (adjust if needed)
        if (!context.Request.Path.StartsWithSegments("/api/auth/verify"))
        {
            await next(context);
            return;
        }

        var clientId = context.Request.Headers["X-Client-Id"].ToString();
        var apiKey = context.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey))
        {
            if (context.Response.HasStarted) return;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { errorCode = "SERVICE_AUTH_MISSING" });
            return;
        }

        var expected = _cfg[$"ServiceAuth:Clients:{clientId}"];

        if (string.IsNullOrWhiteSpace(expected) || !SlowEquals(expected, apiKey))
        {
            if (context.Response.HasStarted) return;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { errorCode = "SERVICE_AUTH_INVALID" });
            return;
        }

        await next(context);
    }

    private static bool SlowEquals(string a, string b)
    {
        // constant-time compare to avoid timing leaks
        if (a.Length != b.Length) return false;

        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }
}