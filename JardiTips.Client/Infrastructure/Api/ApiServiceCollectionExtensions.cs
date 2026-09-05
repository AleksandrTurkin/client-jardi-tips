using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Application.Coordination;
using JardiTips.Client.Infrastructure.Authentication;
using JardiTips.Client.Infrastructure.IndexedDb;
using Microsoft.AspNetCore.Components.Authorization;

namespace JardiTips.Client.Infrastructure.Api;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .Validate(
                options => ApiOptions.TryCreateBaseUri(options.BaseUrl, out _),
                $"{ApiOptions.SectionName}:BaseUrl must be an absolute HTTP or HTTPS URL.");

        services.AddScoped<HttpClient>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiOptions>>()
                .Value;

            return new HttpClient
            {
                BaseAddress = options.CreateBaseUri()
            };
        });
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IAuthenticationService>(serviceProvider =>
            serviceProvider.GetRequiredService<AuthenticationService>());
        services.AddScoped<IAccessTokenProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<AuthenticationService>());
        services.AddScoped<ClientAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<ClientAuthenticationStateProvider>());
        services.AddScoped<IApiClient, ApiClient>();
        services.AddScoped<IAuthenticationApiSource, AuthenticationApiSource>();
        services.AddScoped<ICategoryApiSource, CategoryApiSource>();
        services.AddScoped<ITipApiSource, TipApiSource>();
        services.AddScoped<BrowserDatabase>();
        services.AddScoped<IBrowserDataCleaner>(serviceProvider =>
            serviceProvider.GetRequiredService<BrowserDatabase>());
        services.AddScoped<IAuthenticationStore, IndexedDbAuthenticationStore>();
        services.AddScoped<ICategoryStore, IndexedDbCategoryStore>();
        services.AddScoped<ITipStore, IndexedDbTipStore>();
        services.AddScoped<CategoryQueryService>();
        services.AddScoped<ICategoryQueries>(serviceProvider =>
            serviceProvider.GetRequiredService<CategoryQueryService>());
        services.AddScoped<ICategoryStartup>(serviceProvider =>
            serviceProvider.GetRequiredService<CategoryQueryService>());
        services.AddScoped<TipQueryService>();
        services.AddScoped<ITipQueries>(serviceProvider =>
            serviceProvider.GetRequiredService<TipQueryService>());

        return services;
    }
}
