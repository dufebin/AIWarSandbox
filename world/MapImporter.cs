using System.Threading.Tasks;
using Godot;

namespace AIWarSandbox.World;

/// <summary>
/// Downloads Gaode (AutoNavi) satellite tiles and AWS Terrarium elevation tiles
/// for a <see cref="MapSource"/> bounding box, stitches them into a single
/// satellite <see cref="ImageTexture"/> and a Terrarium-decoded <see cref="float"/> heightmap.
/// Tiles are cached under <see cref="MapSource.CacheDir"/>; partial failures are
/// tolerated (missing tile -> 0 height, gray satellite pixel block).
/// </summary>
public partial class MapImporter : Node
{
    private static readonly string[] GaodeHosts = { "webst01", "webst02", "webst03", "webst04" };

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    public MapSource Source { get; set; } = null!;

    /// <summary>
    /// Fetch all tiles in the source bbox, returning the stitched satellite texture,
    /// the decoded heightmap, and its dimensions (width, height in *pixels*).
    /// </summary>
    public async Task<(ImageTexture satellite, float[,] heightmap, int width, int height)> ImportAsync()
    {
        Source ??= MapSource.CreateDefault();

        // Tile range covering the bbox.
        var (minX, maxY) = GeoUtil.LatLngToTile(Source.NorthLat, Source.WestLng, Source.Zoom);
        var (maxX, minY) = GeoUtil.LatLngToTile(Source.SouthLat, Source.EastLng, Source.Zoom);

        // Ensure proper ordering (LatLngToTile clamps so this is just defensive).
        if (minX > maxX) (minX, maxX) = (maxX, minX);
        if (minY > maxY) (minY, maxY) = (maxY, minY);

        int tilesX = maxX - minX + 1;
        int tilesY = maxY - minY + 1;

        const int TilePx = 256; // Gaode + Terrarium tiles are 256x256.

        // Stitched satellite image.
        var satelliteImage = Image.CreateEmpty(tilesX * TilePx, tilesY * TilePx, false, Image.Format.Rgba8);
        satelliteImage.Fill(Colors.Gray);

        // Heightmap sized to match satellite pixels (1 sample per pixel).
        float[,] heightmap = new float[tilesX * TilePx, tilesY * TilePx];

        // Ensure cache dir exists.
        DirAccess.MakeDirRecursiveAbsolute(Source.CacheDir);

        // Fetch every tile. Each tile is fetched sequentially-ish via awaited signals;
        // we still spawn one HttpRequest child per concurrent tile to keep it simple
        // and avoid socket churn. Concurrency is bounded to a small window to be polite.
        const int MaxConcurrent = 4;

        var pending = new System.Collections.Generic.List<Task>();
        int inflight = 0;
        int cursor = 0;

        int totalTiles = tilesX * tilesY;
        int tileIndex = 0;

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                int xTile = minX + tx;
                int yTile = minY + ty;
                int px = tx * TilePx;
                int py = ty * TilePx;
                int idx = tileIndex++;

                // Throttle concurrency.
                while (inflight >= MaxConcurrent)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    // Drop completed tasks.
                    pending.RemoveAll(t => t.IsCompleted);
                    inflight = pending.Count;
                }

                pending.RemoveAll(t => t.IsCompleted);
                inflight = pending.Count;

                var t = FetchTilePair(xTile, yTile, px, py, TilePx, satelliteImage, heightmap, idx, totalTiles);
                pending.Add(t);
                inflight = pending.Count;
            }
        }

        // Drain remaining.
        foreach (var t in pending)
            await t;

        var texture = ImageTexture.CreateFromImage(satelliteImage);
        return (texture, heightmap, tilesX * TilePx, tilesY * TilePx);
    }

    private async Task FetchTilePair(int x, int y, int px, int py, int tilePx,
        Image satelliteImage, float[,] heightmap, int idx, int total)
    {
        // Satellite first (required), elevation second (optional, controlled by UseRealTerrain).
        await FetchSatelliteTile(x, y, px, py, tilePx, satelliteImage);

        if (Source.UseRealTerrain)
        {
            await FetchElevationTile(x, y, px, py, tilePx, heightmap);
        }

        if ((idx + 1) % 8 == 0 || idx + 1 == total)
            GD.Print($"[MapImporter] tile {idx + 1}/{total} done");
    }

    private async Task FetchSatelliteTile(int x, int y, int px, int py, int tilePx, Image dst)
    {
        string host = GaodeHosts[(x + y) % GaodeHosts.Length];
        string url = $"https://{host}.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={Source.Zoom}";
        string cachePath = $"{Source.CacheDir}/sat_{Source.Zoom}_{x}_{y}.png";

        Image? img = await LoadCachedOrDownloadImage(url, cachePath,
            headers: new string[] { "Referer: https://www.amap.com/", $"User-Agent: {BrowserUserAgent}" });

        if (img == null)
        {
            // Leave the gray fill already present.
            return;
        }

        // Resize defensively if a tile came back at a different size.
        if (img.GetWidth() != tilePx || img.GetHeight() != tilePx)
            img.Resize(tilePx, tilePx);

        dst.BlendRect(img, new Rect2I(0, 0, tilePx, tilePx), new Vector2I(px, py));
    }

    private async Task FetchElevationTile(int x, int y, int px, int py, int tilePx, float[,] heightmap)
    {
        string url = $"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{Source.Zoom}/{x}/{y}.png";
        string cachePath = $"{Source.CacheDir}/ele_{Source.Zoom}_{x}_{y}.png";

        Image? img = await LoadCachedOrDownloadImage(url, cachePath, headers: null);
        if (img == null)
        {
            // Missing elevation -> 0 height already default in the array.
            return;
        }

        if (img.GetWidth() != tilePx || img.GetHeight() != tilePx)
            img.Resize(tilePx, tilePx);

        // Decode Terrarium RGB -> meters into the heightmap array.
        for (int dy = 0; dy < tilePx; dy++)
        {
            for (int dx = 0; dx < tilePx; dx++)
            {
                Color c = img.GetPixel(dx, dy);
                double meters = GeoUtil.DecodeTerrariumHeight(c);
                heightmap[px + dx, py + dy] = (float)meters * Source.HeightScale;
            }
        }
    }

    /// <summary>
    /// Try loading the cached PNG; on miss, download via a child HttpRequest node,
    /// cache the bytes, then load. Returns null on any failure.
    /// </summary>
    private async Task<Image?> LoadCachedOrDownloadImage(string url, string cachePath, string[]? headers)
    {
        // 1. Try cache.
        if (FileAccess.FileExists(cachePath))
        {
            var cached = Image.LoadFromFile(cachePath);
            if (cached != null)
                return cached;
        }

        // 2. Download.
        var req = new HttpRequest { Name = $"Req_{cachePath.GetHashCode()}" };
        req.UseThreads = true;
        AddChild(req);

        string[] sendHeaders = headers ?? System.Array.Empty<string>();

        var err = req.Request(url, sendHeaders, HttpClient.Method.Get, "");
        if (err != Error.Ok)
        {
            req.QueueFree();
            return null;
        }

        // Await the request_completed signal.
        var signal = await ToSignal(req, HttpRequest.SignalName.RequestCompleted);
        // signal = (long result, long responseCode, string[] headers, byte[] body)
        Variant[] args = signal;
        long result = (long)args[0];
        long responseCode = (long)args[1];
        byte[] body = args[3].AsByteArray();

        req.QueueFree();

        if (result != (long)HttpRequest.Result.Success || body == null || body.Length == 0)
        {
            GD.PushWarning($"[MapImporter] download failed: {url} result={result} code={responseCode}");
            return null;
        }

        // 3. Persist to cache.
        using (var fa = FileAccess.Open(cachePath, FileAccess.ModeFlags.Write))
        {
            if (fa != null)
                fa.StoreBuffer(body);
        }

        // 4. Decode.
        var img = new Image();
        Error loadErr = img.LoadPngFromBuffer(body);
        if (loadErr != Error.Ok)
        {
            GD.PushWarning($"[MapImporter] PNG decode failed: {url} err={loadErr}");
            return null;
        }
        return img;
    }
}
