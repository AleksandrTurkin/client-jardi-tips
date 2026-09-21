using System.Collections.Immutable;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;

namespace JardiTips.Client.Features.Home.Coordination;

internal sealed class HomeCategoryLoader(
    ICategoryQueries categoryQueries,
    IUserCategoryQueries userCategoryQueries)
{
    private const int PageSize = 100;

    public async Task<ImmutableArray<CategoryDto>> LoadPublicCategoriesAsync(CancellationToken cancellationToken)
    {
        var loadedCategories = new List<CategoryDto>();
        var visitedPageContexts = new HashSet<string>(StringComparer.Ordinal);
        string? pageContext = null;

        do
        {
            var page = await categoryQueries.GetAsync(
                new CategoriesFilter(pageContext, PageSize),
                cancellationToken);

            loadedCategories.AddRange(page.Data);
            pageContext = page.PageContext;

            if (pageContext is not null && !visitedPageContexts.Add(pageContext))
                throw new InvalidOperationException("The categories query returned a repeated page cursor.");
        }
        while (pageContext is not null);

        return loadedCategories
            .OrderByDescending(category => category.UpdatedAt)
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<CategoryDto>> LoadUserCollectionsAsync(CancellationToken cancellationToken)
    {
        var loadedCategories = new List<CategoryDto>();
        var visitedPageContexts = new HashSet<string>(StringComparer.Ordinal);
        CategoryDto? favorite = null;
        string? pageContext = null;

        do
        {
            var page = await userCategoryQueries.GetAsync(
                new CategoriesFilter(pageContext, PageSize),
                cancellationToken);

            if (pageContext is null)
                favorite = page.Favorite;

            loadedCategories.AddRange(page.Categories.Data);
            pageContext = page.Categories.PageContext;

            if (!string.IsNullOrWhiteSpace(pageContext) && !visitedPageContexts.Add(pageContext))
                throw new InvalidOperationException("The user categories query returned a repeated page cursor.");
        }
        while (!string.IsNullOrWhiteSpace(pageContext));

        var collections = new List<CategoryDto>(loadedCategories.Count + (favorite is null ? 0 : 1));
        if (favorite is not null)
            collections.Add(favorite);

        collections.AddRange(loadedCategories);
        return collections.ToImmutableArray();
    }
}
