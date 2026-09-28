using System.Net;
using System.Security.Claims;
using JardiTips.Client.Application.Abstractions;
using JardiTips.Client.Features.Tips.Models;
using JardiTips.Client.Infrastructure.Api;

namespace JardiTips.Client.Features.Tips.Coordination;

public sealed class TipManagementCoordinator(
    ITipApiSource tipApiSource,
    ITipQueries tipQueries,
    IUserCategoryQueries userCategories,
    IAuthenticationService authentication,
    IBrowserConnectivity connectivity,
    ILogger<TipManagementCoordinator> logger)
{
    public bool IsOnline => connectivity.IsOnline;

    public Task<TipManagementResult> CreateAsync(CreateTipRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(request.CategoryId, token => tipApiSource.CreateAsync(request, token), cancellationToken);

    public Task<TipManagementResult> UpdateAsync(
        TipDto tip, UpdateTipRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(tip.CategoryId, async token =>
        {
            await tipApiSource.UpdateAsync(tip.Id, request, token);
            return TipCreateOutcome.Created;
        }, cancellationToken);

    public Task<TipManagementResult> DeleteAsync(TipDto tip, CancellationToken cancellationToken) =>
        ExecuteAsync(tip.CategoryId, async token =>
        {
            await tipApiSource.DeleteAsync(tip.Id, token);
            return TipCreateOutcome.Created;
        }, cancellationToken);

    private async Task<TipManagementResult> ExecuteAsync(
        Guid categoryId,
        Func<CancellationToken, Task<TipCreateOutcome>> mutate,
        CancellationToken cancellationToken)
    {
        var userId = authentication.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!authentication.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
            return new(TipManagementStatus.Unauthorized);
        if (!connectivity.IsOnline)
            return new(TipManagementStatus.Offline);

        try
        {
            var outcome = await mutate(cancellationToken);
            if (outcome == TipCreateOutcome.LimitReached)
                return new(TipManagementStatus.LimitReached);

            var refreshed = await ReconcileAsync(categoryId, userId);
            return IsCurrentUser(userId)
                ? new(TipManagementStatus.Succeeded, Refreshed: refreshed)
                : new(TipManagementStatus.Cancelled);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReconcileAsync(categoryId, userId);
            return new(TipManagementStatus.Cancelled);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(TipManagementStatus.Unauthorized);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new(TipManagementStatus.NotFound);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.BadRequest || exception.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return new(TipManagementStatus.Invalid, Message: exception.ProblemDetails.Detail, Errors: exception.ProblemDetails.Errors);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Tip mutation failed for category {CategoryId}.", categoryId);
            return new(TipManagementStatus.Failed);
        }
    }

    private bool IsCurrentUser(string userId) =>
        string.Equals(userId, authentication.CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value, StringComparison.Ordinal);

    private async Task<bool> ReconcileAsync(Guid categoryId, string userId)
    {
        try
        {
            await tipQueries.InvalidateAsync(categoryId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Tip cache could not be invalidated for category {CategoryId}.", categoryId);
            return false;
        }

        if (!IsCurrentUser(userId))
            return false;

        try
        {
            await userCategories.RefreshAsync(CancellationToken.None);
            return IsCurrentUser(userId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Tip mutation completed but collection {CategoryId} could not be refreshed.", categoryId);
            return false;
        }
    }
}
