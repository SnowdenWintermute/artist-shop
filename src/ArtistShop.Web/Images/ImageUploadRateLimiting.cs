using System.Security.Claims;
using System.Threading.RateLimiting;

namespace ArtistShop.Web.Images;

public static class ImageUploadRateLimiting
{
    public const string PolicyName = "image-uploads";

    // a courtesy cap, not the real protection — ImageProcessingLimiter is what keeps the machine
    // up. A bulk run sends three or four files at a time, so this only catches a runaway client
    private const int BurstRequests = 100;
    private const int RequestsPerSecond = 10;

    public static IServiceCollection AddImageUploadRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(
                PolicyName,
                // a partition is one bucket of tokens; the key decides which requests share it
                httpContext =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        UploaderKey(httpContext),
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = BurstRequests,
                            TokensPerPeriod = RequestsPerSecond,
                            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                            // turned away at once instead of held open waiting for a token
                            QueueLimit = 0,
                        }
                    )
            );

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, cancellationToken) =>
            {
                // the limiter knows when the next token arrives; the client waits that long
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)
                        Math.Ceiling(retryAfter.TotalSeconds)
                    ).ToString();
                }

                return ValueTask.CompletedTask;
            };
        });

    // authentication runs ahead of this middleware, so the claim is there for a signed-in artist;
    // the address keeps anonymous requests from sharing one bucket
    private static string UploaderKey(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
