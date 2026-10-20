using MissilePredictor.TestKit.Fakes;

namespace MissilePredictor.Tests.App;

/// <summary>An <see cref="OfflineAppFactory"/> wired to a running <see cref="FakeNeptunServer"/>, shared by a test class.</summary>
public sealed class OfflineAppFixture : IAsyncLifetime
{
    public FakeNeptunServer NeptunServer { get; private set; } = null!;

    /// <summary>
    /// The app's single stream connection, accepted once at startup. Boot tests send their frames through it
    /// (the app connects only once, so waiting for another connection would hang).
    /// </summary>
    public FakeNeptunConnection Neptun { get; private set; } = null!;
    public OfflineAppFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        NeptunServer = await FakeNeptunServer.StartAsync();
        Factory = new OfflineAppFactory(NeptunServer.Url);

        // Touching Services boots the host, which makes the stream service connect.
        _ = Factory.Services;
        Neptun = await NeptunServer.NextConnectionAsync(TimeSpan.FromSeconds(10));
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await NeptunServer.DisposeAsync();
    }
}
