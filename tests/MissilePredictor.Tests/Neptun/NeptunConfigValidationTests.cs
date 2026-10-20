using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MissilePredictor.API.Neptun;

namespace MissilePredictor.Tests.Neptun;

public class NeptunConfigValidationTests
{
    [Fact]
    public void Binds_ListOfAreas()
    {
        var config = Resolve(
            Area(0, "Львів", 49.8397, 24.0297, 10),
            Area(1, "Київ", 50.4501, 30.5234, 15));

        Assert.Equal(2, config.Areas.Count);
        Assert.Equal("Київ", config.Areas[1].Title);
        Assert.Equal(15, config.Areas[1].RadiusKm);
    }

    [Fact]
    public void Fails_WhenNoAreas()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Resolve());

        Assert.Contains("Areas", ex.Message);
    }

    [Fact]
    public void Fails_WhenRadiusNotPositive()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Resolve(Area(0, "Львів", 49.8, 24.0, 0)));

        Assert.Contains("Neptun:Areas[0]", ex.Message);
    }

    [Fact]
    public void Fails_WhenLatitudeOutOfRange()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Resolve(Area(0, "Львів", 95, 24.0, 10)));

        Assert.Contains("Neptun:Areas[0]", ex.Message);
    }

    [Fact]
    public void Fails_WhenTitlesDuplicate()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Resolve(
            Area(0, "Львів", 49.8, 24.0, 10),
            Area(1, "львів", 50.4, 30.5, 10)));

        Assert.Contains("duplicate", ex.Message);
    }

    [Fact]
    public void From_DefaultsToTitle()
    {
        var config = Resolve(Area(0, "Львів", 49.8, 24.0, 10));

        Assert.Equal("Львів", config.Areas[0].ToArea().From);
    }

    private static Dictionary<string, string?> Area(int i, string title, double lat, double lon, double radius) => new()
    {
        [$"Neptun:Areas:{i}:Title"] = title,
        [$"Neptun:Areas:{i}:Latitude"] = lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [$"Neptun:Areas:{i}:Longitude"] = lon.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [$"Neptun:Areas:{i}:RadiusKm"] = radius.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static NeptunConfig Resolve(params Dictionary<string, string?>[] areas)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(areas.SelectMany(a => a))
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<NeptunConfig>().Bind(configuration.GetSection("Neptun"));
        services.AddSingleton<IValidateOptions<NeptunConfig>, NeptunConfigValidator>();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<NeptunConfig>>().Value;
    }
}
