using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using JardiTips.Client;
using JardiTips.Client.Infrastructure.Api;
using JardiTips.Client.Application.Abstractions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddApiInfrastructure(builder.Configuration);
builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var host = builder.Build();
await host.Services.GetRequiredService<IAuthenticationService>().InitializeAsync();
await host.Services.GetRequiredService<ICategoryStartup>().InitializeAsync();
await host.RunAsync();
