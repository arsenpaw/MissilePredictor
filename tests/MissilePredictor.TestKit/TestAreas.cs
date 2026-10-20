using MissilePredictor.API.Areas;

namespace MissilePredictor.TestKit;

/// <summary>Well-known areas shared by tests; the config default lives in appsettings.json.</summary>
public static class TestAreas
{
    public static readonly Area Lviv = new("Львів", 49.8397, 24.0297, 10, "Львову");
    public static readonly Area Kyiv = new("Київ", 50.4501, 30.5234, 10, "Києва");
}
