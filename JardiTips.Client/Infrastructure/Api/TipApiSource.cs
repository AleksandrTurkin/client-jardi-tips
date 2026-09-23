using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Categories.Models;
using JardiTips.Client.Features.Tips.Models;

namespace JardiTips.Client.Infrastructure.Api;

public sealed class TipApiSource(IApiClient apiClient) : ITipApiSource
{
    private const int PageSize = 20;
    private const string LimitReachedCode = "tip-category-limit-reached";

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

    public async Task<TipCreateOutcome> CreateAsync(
        CreateTipRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await apiClient.PostAsync<CreateTipRequest, Guid>("tips", request, cancellationToken);
            return TipCreateOutcome.Created;
        }
        catch (ApiException exception) when (string.Equals(
            exception.ProblemDetails.Code,
            LimitReachedCode,
            StringComparison.Ordinal))
        {
            return TipCreateOutcome.LimitReached;
        }
    }
}
