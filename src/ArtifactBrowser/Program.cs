using System.Threading.RateLimiting;
using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Components;
using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Nefarius.Utilities.AspNetCore;

var builder = WebApplication.CreateBuilder(args).Setup();

builder.Services.AddOptions<ArtifactBrowserOptions>()
    .Bind(builder.Configuration.GetSection(ArtifactBrowserOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var requestTimeoutSeconds = builder.Configuration.GetSection(ArtifactBrowserOptions.SectionName)
    .GetValue(nameof(ArtifactBrowserOptions.RequestTimeoutSeconds), 30);
builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy("files", TimeSpan.FromSeconds(Math.Max(1, requestTimeoutSeconds)));
});

// Persist Data Protection keys under CacheRoot (already a writable, volume-backed directory)
// instead of the container's ephemeral home directory, so keys survive container recreation
// and can be shared across replicas if the app is ever scaled out.
var cacheRootForKeys = builder.Configuration.GetSection(ArtifactBrowserOptions.SectionName)
    .GetValue(nameof(ArtifactBrowserOptions.CacheRoot), "/cache")!;
builder.Services.AddDataProtection()
    .SetApplicationName("ArtifactBrowser")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(cacheRootForKeys, "dp-keys")));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PathGuard>();
builder.Services.AddScoped<FileSystemBrowser>();
builder.Services.AddScoped<PreviewService>();
builder.Services.AddSingleton<ThumbnailService>();
builder.Services.AddSingleton<ZipStreamer>();

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiterOptions.AddPolicy("files", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 120,
                QueueLimit = 0,
            }));

    rateLimiterOptions.AddPolicy("heavy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(30),
                PermitLimit = 20,
                QueueLimit = 5,
            }));
});

// Add services to the container.
builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap.Add("artifactFile", typeof(ExistingArtifactFileConstraint));
});

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build().Setup();

var contentOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ArtifactBrowserOptions>>().Value;
Directory.CreateDirectory(contentOptions.ContentRoot);
Directory.CreateDirectory(contentOptions.CacheRoot);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();
app.Use((context, next) =>
{
    ClientAbortToken.Capture(context);
    return next();
});
app.UseRequestTimeouts();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).ExcludeFromDescription();
app.MapGet("/api/config", (Microsoft.Extensions.Options.IOptions<ArtifactBrowserOptions> options) =>
    Results.Ok(new UiConfigDto
    {
        HeaderTitle = options.Value.ResolvedHeaderTitle,
        DocumentTitle = options.Value.ResolvedDocumentTitle,
        ShowGitHubLink = options.Value.ShowGitHubLink,
    })).ExcludeFromDescription();

app.UseMiddleware<FilesExceptionMiddleware>();
app.MapFilesEndpoints()
    .RequireRateLimiting("files")
    .WithRequestTimeout("files");

// Pretty URLs that map to a real file (including hidden/dotfile names) are served as downloads
// so curl and scripts keep working. Directories and missing paths fall through to the SPA.
app.MapDirectArtifactFiles()
    .RequireRateLimiting("files")
    .WithRequestTimeout("files");

// MapStaticAssets() deliberately gives non-fingerprinted "logical" asset paths (e.g. the
// unfingerprinted "_framework/blazor.web.js" referenced by App.razor) lower routing priority
// than app-defined routes, so that a conventional route can intentionally shadow a static file.
// Home.razor's "/{*Path}" catch-all (needed for deep-linking to arbitrary artifact paths, which
// routinely contain dots, so ":nonfile" isn't an option) would otherwise win that priority contest
// and swallow requests like "/_framework/blazor.web.js", serving the SPA shell (200 text/html)
// instead of the actual script. Force static assets to the highest priority so they always win.
app.MapStaticAssets().Add(endpointBuilder =>
{
    if (endpointBuilder is RouteEndpointBuilder routeEndpointBuilder)
    {
        routeEndpointBuilder.Order = int.MinValue;
    }
});
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ArtifactBrowser.Client._Imports).Assembly);

app.Run();

// Exposes the generated top-level Program as a public partial class so that
// WebApplicationFactory<Program> can be used from the integration test project.
public partial class Program;
