using System.Diagnostics;
using System.Net;
using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtifactBrowser.Tests;

public sealed class RequestTimeoutTests
{
    [Fact]
    public async Task FileRequest_WhenTimeoutExpires_Returns504()
    {
        // RequestTimeoutsMiddleware is a no-op while a debugger is attached.
        if (Debugger.IsAttached)
        {
            return;
        }

        using var root = new TempContentRoot();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ArtifactBrowser:ContentRoot"] = root.ContentRoot,
                    ["ArtifactBrowser:CacheRoot"] = root.CacheRoot,
                });
            });
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<RequestTimeoutOptions>(options =>
                {
                    options.AddPolicy("files", TimeSpan.FromMilliseconds(100));
                });
                services.AddSingleton<IRequestHold, BoundedRequestHold>();
            });
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/files/list?path=");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    private sealed class BoundedRequestHold : IRequestHold
    {
        public Task HoldAsync(CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}
