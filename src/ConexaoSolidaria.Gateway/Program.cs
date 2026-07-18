using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Prometheus;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks and service discovery.
builder.AddServiceDefaults();

// YARP reverse proxy configured from appsettings ("ReverseProxy" section).
// AddServiceDiscoveryDestinationResolver resolves cluster destinations declared
// with logical resource names (e.g. http://identity-api) through Aspire service discovery.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(context =>
    {
        // Propagate a correlation id to downstream services (creates one if absent).
        context.AddRequestTransform(transform =>
        {
            const string headerName = "X-Correlation-Id";
            if (!transform.ProxyRequest.Headers.Contains(headerName))
            {
                var correlationId = transform.HttpContext.Request.Headers[headerName].FirstOrDefault()
                    ?? Guid.NewGuid().ToString("N");
                transform.ProxyRequest.Headers.TryAddWithoutValidation(headerName, correlationId);
            }

            return default;
        });
    });

// #6 Rate limiting: the gateway is the single entry point, so it is the ideal place
// to protect downstream services from abuse. Partitioned per client IP.
builder.Services.AddRateLimiter(options =>
{
    // Named policy applied to the "auth" route (login/cadastro): most restrictive.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Named policy applied to the "doacoes" route: moderate.
    options.AddPolicy("donation", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Global safety net for every request (e.g. campanhas), also per IP.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Reject with 429 + Retry-After + a minimal ProblemDetails payload.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)retryAfter.TotalSeconds
            : 60;
        response.Headers.RetryAfter = retryAfterSeconds.ToString();

        response.ContentType = "application/problem+json";
        await response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = $"Limite de requisições excedido. Tente novamente em {retryAfterSeconds} segundos."
        }, cancellationToken);
    };
});

var app = builder.Build();

// #7 Security headers: injected on every response before the request reaches the proxy.
// The gateway only serves /api/* (JSON), so a strict CSP is safe here and does not
// affect the Blazor Web app (a separate service).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";
    await next();
});

// HSTS outside Development. Instructs clients to always use HTTPS.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// TODO: app.UseHttpsRedirection() intentionally omitted. Behind Aspire the gateway is
// reached over internal HTTP (service discovery + health probes); enabling HTTPS
// redirection would return 307 for those internal HTTP calls and break them. Revisit
// once a public HTTPS ingress terminates TLS in front of the gateway.

// Enforce rate limiting before the proxy forwards the request.
app.UseRateLimiter();

// Expose inbound HTTP metrics (RPS/latency of the single entry point) for Prometheus.
app.UseHttpMetrics();

// Health endpoints (/health, /alive) from ServiceDefaults.
app.MapDefaultEndpoints();

// Prometheus scrape endpoint (/metrics). Not proxied: YARP only routes /api/*.
app.MapMetrics();

// Reverse proxy pipeline: single entry point for all downstream APIs.
app.MapReverseProxy();

app.Run();
