using MissilePredictor.TestKit.Fakes;

namespace MissilePredictor.Tests.App;

/// <summary>An <see cref="OfflineAppFactory"/> wired to a running <see cref="FakeNeptunServer"/>, shared by a test class.</summary>
public sealed class OfflineAppFixture : IAsyncLifetime
{
    public FakeNeptunServer Neptun { get; private set; } = null!;
    public OfflineAppFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Neptun = await FakeNeptunServer.StartAsync();
        Factory = new OfflineAppFactory(Neptun.Url);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Neptun.DisposeAsync();
    }
}
