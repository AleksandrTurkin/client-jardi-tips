namespace JardiTips.Client.Features.Home.Coordination;

internal sealed class HomeRequestLifetime : IAsyncDisposable
{
    private readonly object lifecycleLock = new();
    private CancellationTokenSource? sessionCancellation;
    private CancellationTokenSource? userLoadCancellation;
    private long userLoadGeneration;
    private bool disposed;

    public bool IsDisposed
    {
        get
        {
            lock (lifecycleLock)
                return disposed;
        }
    }

    public CancellationToken BeginSession()
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            sessionCancellation ??= new CancellationTokenSource();
            return sessionCancellation.Token;
        }
    }

    public HomeUserLoadRequest BeginUserLoad(string userId)
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var sessionToken = BeginSession();
            var previous = userLoadCancellation;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
            userLoadCancellation = cancellation;
            userLoadGeneration++;

            if (previous is not null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            return new HomeUserLoadRequest(userLoadGeneration, userId, cancellation, previous);
        }
    }

    public void CompleteUserLoad(HomeUserLoadRequest request)
    {
    }

    public bool IsCurrent(HomeUserLoadRequest request)
    {
        lock (lifecycleLock)
            return !disposed
                && request.Generation == userLoadGeneration
                && ReferenceEquals(userLoadCancellation, request.Cancellation);
    }

    public async Task EndSessionAsync()
    {
        CancellationTokenSource? session;
        CancellationTokenSource? userLoad;

        lock (lifecycleLock)
        {
            if (disposed)
                return;

            session = sessionCancellation;
            sessionCancellation = null;
            userLoad = userLoadCancellation;
            userLoadCancellation = null;
            userLoadGeneration++;
        }

        if (userLoad is not null)
        {
            await userLoad.CancelAsync();
            userLoad.Dispose();
        }

        if (session is not null)
        {
            await session.CancelAsync();
            session.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? session;
        CancellationTokenSource? userLoad;

        lock (lifecycleLock)
        {
            if (disposed)
                return;

            disposed = true;
            session = sessionCancellation;
            sessionCancellation = null;
            userLoad = userLoadCancellation;
            userLoadCancellation = null;
            userLoadGeneration++;
        }

        if (userLoad is not null)
        {
            await userLoad.CancelAsync();
            userLoad.Dispose();
        }

        if (session is not null)
        {
            await session.CancelAsync();
            session.Dispose();
        }
    }
}

internal sealed record HomeUserLoadRequest(
    long Generation,
    string UserId,
    CancellationTokenSource Cancellation,
    CancellationTokenSource? PreviousCancellation)
{
    public CancellationToken Token => Cancellation.Token;
}
