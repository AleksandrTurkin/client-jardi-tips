using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class CategoryApiSource(IApiClient apiClient) : ICategoryApiSource
{
    private const int MaximumPageSize = 100;

    public Task<PagedResult<CategoryDto>> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(filter.PageContext))
            query.Add($"pageContext={Uri.EscapeDataString(filter.PageContext)}");

        if (filter.Limit is not null)
            query.Add($"limit={Math.Clamp(filter.Limit.Value, 1, MaximumPageSize)}");

        var route = query.Count == 0
            ? "categories"
            : $"categories?{string.Join('&', query)}";

        return apiClient.GetAsync<PagedResult<CategoryDto>>(route, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var categories = new List<CategoryDto>();
        var visitedPageContexts = new HashSet<string>(StringComparer.Ordinal);
        string? pageContext = null;

        do
        {
            var page = await GetAsync(
                new CategoriesFilter(pageContext, MaximumPageSize),
                cancellationToken);

            categories.AddRange(page.Data);

            if (!string.IsNullOrWhiteSpace(page.PageContext)
                && !visitedPageContexts.Add(page.PageContext))
                throw new InvalidOperationException("The categories API returned a repeated page cursor.");

            pageContext = page.PageContext;
        }
        while (!string.IsNullOrWhiteSpace(pageContext));

        return categories;
    }
}
