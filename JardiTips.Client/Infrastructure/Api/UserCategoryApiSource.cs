using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class UserCategoryApiSource(IApiClient apiClient) : IUserCategoryApiSource
{
    private const int MaximumPageSize = 100;

    public Task<Guid> CreateAsync(string name, string description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return apiClient.PostAsync<CreateCategoryRequest, Guid>(
            "categories",
            new CreateCategoryRequest(name, description),
            cancellationToken);
    }

    public Task UpdateAsync(Guid id, string name, string description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return apiClient.PutAsync(
            $"categories/{id}",
            new UpdateCategoryRequest(name, description),
            cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return apiClient.DeleteAsync($"categories/{id}", cancellationToken);
    }

    public Task<UserCategoriesDto> GetAsync(
        CategoriesFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();

        var query = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(filter.PageContext))
            query.Add($"pageContext={Uri.EscapeDataString(filter.PageContext)}");

        if (filter.Limit is not null)
            query.Add($"limit={Math.Clamp(filter.Limit.Value, 1, MaximumPageSize)}");

        var route = query.Count == 0
            ? "user/categories"
            : $"user/categories?{string.Join('&', query)}";

        return apiClient.GetAsync<UserCategoriesDto>(route, cancellationToken);
    }

    public async Task<UserCategoriesDto> GetAllAsync(CancellationToken cancellationToken)
    {
        var categories = new List<CategoryDto>();
        var visitedPageContexts = new HashSet<string>(StringComparer.Ordinal);
        CategoryDto? favorite = null;
        string? pageContext = null;

        do
        {
            var page = await GetAsync(
                new CategoriesFilter(pageContext, MaximumPageSize),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (pageContext is null)
                favorite = page.Favorite;

            categories.AddRange(page.Categories.Data);

            if (!string.IsNullOrWhiteSpace(page.Categories.PageContext)
                && !visitedPageContexts.Add(page.Categories.PageContext))
                throw new InvalidOperationException("The user categories API returned a repeated page cursor.");

            pageContext = page.Categories.PageContext;
        }
        while (!string.IsNullOrWhiteSpace(pageContext));

        return new UserCategoriesDto(favorite, new PagedResult<CategoryDto>(null, categories));
    }
}
