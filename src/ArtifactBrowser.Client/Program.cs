using ArtifactBrowser.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(60),
});

builder.Services.AddScoped<FilesApiClient>();
builder.Services.AddScoped<PreferencesService>();
builder.Services.AddScoped<SelectionService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<DownloadService>();

await builder.Build().RunAsync();
