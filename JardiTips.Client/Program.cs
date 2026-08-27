using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using JardiTips.Client;
using JardiTips.Client.Infrastructure.Api;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddApiInfrastructure(builder.Configuration);
builder.Services.AddMudServices();

await builder.Build().RunAsync();
