using Godot;

namespace AIWarSandbox.World;

/// <summary>
/// Static helpers for Web Mercator slippy-map tile math and Terrarium elevation decoding.
/// </summary>
public static class GeoUtil
{
    /// <summary>
    /// Convert lat/lng (degrees) to slippy-map tile indices at zoom z.
    /// Uses the standard Web Mercator formulas:
    ///   n = 2^z
    ///   x = floor((lng + 180) / 360 * n)
    ///   y = floor((1 - asinh(tan(lat * π/180)) / π) / 2 * n)
    /// </summary>
    public static (int x, int y) LatLngToTile(double lat, double lng, int z)
    {
        double n = System.Math.Pow(2.0, z);
        double x = System.Math.Floor((lng + 180.0) / 360.0 * n);

        double latRad = lat * System.Math.PI / 180.0;
        double tan = System.Math.Tan(latRad);
        // Clamp tan to avoid infinities at the poles.
        tan = System.Math.Clamp(tan, -1e15, 1e15);
        double asinh = System.Math.Log(tan + System.Math.Sqrt(tan * tan + 1.0));
        double y = System.Math.Floor((1.0 - asinh / System.Math.PI) / 2.0 * n);

        // Clamp into valid tile range.
        int xi = (int)System.Math.Clamp(x, 0.0, n - 1.0);
        int yi = (int)System.Math.Clamp(y, 0.0, n - 1.0);
        return (xi, yi);
    }

    /// <summary>
    /// Inverse of <see cref="LatLngToTile"/>: returns the NW corner lat/lng (degrees)
    /// of tile (x, y) at zoom z.
    /// </summary>
    public static Vector2 TileToLatLng(int x, int y, int z)
    {
        double n = System.Math.Pow(2.0, z);
        double lng = (double)x / n * 360.0 - 180.0;

        double ratio = System.Math.PI * (1.0 - 2.0 * (double)y / n);
        double latRad = System.Math.Atan(System.Math.Sinh(ratio));
        double lat = latRad * 180.0 / System.Math.PI;
        return new Vector2((float)lat, (float)lng);
    }

    /// <summary>
    /// Decode a Terrarium-format elevation PNG RGB pixel into meters.
    /// Formula: (R*256 + G)*256 + B - 32768
    /// Channels are normalized 0..1 from a Godot <see cref="Color"/>, so we
    /// scale them back to 0..255 first.
    /// </summary>
    public static double DecodeTerrariumHeight(Color c)
    {
        double r = System.Math.Round(c.R * 255.0);
        double g = System.Math.Round(c.G * 255.0);
        double b = System.Math.Round(c.B * 255.0);
        return (r * 256.0 + g) * 256.0 + b - 32768.0;
    }
}
