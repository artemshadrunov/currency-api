using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Context;

namespace CurrencyConverter.Core.Infrastructure;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var clientId = "anonymous";
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);
                    clientId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "unknown";
                }
            }

            using (LogContext.PushProperty("ClientIP", context.Connection.RemoteIpAddress))
            using (LogContext.PushProperty("ClientId", clientId))
            using (LogContext.PushProperty("Method", context.Request.Method))
            using (LogContext.PushProperty("Path", context.Request.Path))
            using (LogContext.PushProperty("StatusCode", context.Response.StatusCode))
            using (LogContext.PushProperty("ElapsedMilliseconds", sw.ElapsedMilliseconds))
            {
                _logger.LogInformation(
                    "Request completed: {Method} {Path} - Status: {StatusCode} - Time: {ElapsedMilliseconds}ms - Client: {ClientId} ({ClientIP})",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    clientId,
                    context.Connection.RemoteIpAddress);
            }
        }
    }
}