using System.Threading.RateLimiting;
using ArtifactBrowser.Components;
using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Options;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ArtifactBrowserOptions>()
    .Bind(builder.Configuration.GetSection(ArtifactBrowserOptions.SectionName))
    .ValidateDataAnnotations();

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
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

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
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).ExcludeFromDescription();

app.UseMiddleware<FilesExceptionMiddleware>();
app.MapFilesEndpoints()
    .RequireRateLimiting("files");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ArtifactBrowser.Client._Imports).Assembly);

app.Run();

// Exposes the generated top-level Program as a public partial class so that
// WebApplicationFactory<Program> can be used from the integration test project.
public partial class Program;
