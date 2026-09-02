using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class TipApiSource(IApiClient apiClient) : ITipApiSource
{
    private const int PageSize = 20;

    public Task<PagedResult<TipDto>> GetAsync(
        TipsFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = new List<string>(3)
        {
            $"categoryId={Uri.EscapeDataString(filter.CategoryId.ToString("D"))}",
            $"limit={PageSize}"
        };

        if (!string.IsNullOrWhiteSpace(filter.PageContext))
            query.Add($"pageContext={Uri.EscapeDataString(filter.PageContext)}");

        return apiClient.GetAsync<PagedResult<TipDto>>(
            $"tips?{string.Join('&', query)}",
            cancellationToken);
    }
}
