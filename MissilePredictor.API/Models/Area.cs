namespace MissilePredictor.Models;

/// <summary>
/// A circular zone on the map. A target counts as inside when its reported position is
/// within <see cref="RadiusKm"/> of the centre.
/// </summary>
public sealed record Area(string Title, double Latitude, double Longitude, double RadiusKm)
{
    public static readonly Area Lviv = new("Львів", 49.8397, 24.0297, 10);

    private const double EarthRadiusKm = 6371.0;

    public bool Contains(double latitude, double longitude) => DistanceKm(latitude, longitude) <= RadiusKm;

    // Haversine great-circle distance from the centre.
    public double DistanceKm(double latitude, double longitude)
    {
        var dLat = ToRadians(latitude - Latitude);
        var dLon = ToRadians(longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
