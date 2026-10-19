namespace MissilePredictor.TestKit;

/// <summary>Runs <paramref name="dispose"/> on <see cref="DisposeAsync"/>, for <c>await using</c> on ad-hoc cleanup.</summary>
public sealed class AsyncDisposable(Func<Task> dispose) : IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await dispose();
}
