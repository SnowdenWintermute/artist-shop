// Measures how much memory ImageProcessor needs for one image, to check
// ImageProcessing:MemoryEstimateMultiplier and EstimatedTimePerImage in appsettings.json.
// Linux only (it reads /proc). Run it on the machine the app runs on, one image per run:
//
//   MALLOC_ARENA_MAX=2 dotnet run tools/measure-image-memory/measure.cs -- path/to/photo.jpg
//
// On a server with Docker but no .NET SDK, from the repo root:
//
//   docker run --rm -e MALLOC_ARENA_MAX=2 -v "$PWD":/repo -w /repo mcr.microsoft.com/dotnet/sdk:10.0 \
//     dotnet run tools/measure-image-memory/measure.cs -- /repo/path/to/photo.jpg
//
// Use real photos: a portrait phone photo (it carries an EXIF rotation tag, the most expensive case
// measured on 2026-09-17: multiplier 1.44), a large scan, and a PNG. Results from that day are in
// todo.md under the image processing limits.

#:project ../../src/ArtistShop.Web/ArtistShop.Web.csproj

using System.Diagnostics;
using ArtistShop.Web.Images;

// the same libvips settings as Program.cs
NetVips.NetVips.BlockUntrusted = true;
NetVips.NetVips.Concurrency = 1;
NetVips.Cache.Max = 0;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run tools/measure-image-memory/measure.cs -- <image file>");
    return 1;
}

var root = Directory.CreateTempSubdirectory("measure-image-memory-");

try
{
    var storage = new ImageStorage(root.FullName);
    Directory.CreateDirectory(storage.Originals);
    Directory.CreateDirectory(storage.Variants);
    var processor = new ImageProcessor(storage);

    // warm-up: loads libvips and compiles the code path, so the measurement is only the image itself
    var warmUpKey = NewStorageKey();
    using (var warmUp = NetVips.Image.Black(800, 600, bands: 3))
    {
        warmUp.WriteToFile(storage.OriginalPath(warmUpKey) + ".jpg");
    }
    File.Move(storage.OriginalPath(warmUpKey) + ".jpg", storage.OriginalPath(warmUpKey));
    processor.Process(warmUpKey);

    var key = NewStorageKey();
    File.Copy(args[0], storage.OriginalPath(key));
    var header = processor.ReadHeader(key);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var baselineKilobytes = ReadStatusKilobytes("VmRSS");

    // writing 5 resets the kernel's peak memory counter (VmHWM) to the current usage
    File.WriteAllText("/proc/self/clear_refs", "5");

    var stopwatch = Stopwatch.StartNew();
    processor.Process(key);
    stopwatch.Stop();

    var peakMegabytes = (ReadStatusKilobytes("VmHWM") - baselineKilobytes) / 1024.0;
    var decodedMegabytes = header.DecodedBytes / 1024.0 / 1024.0;

    Console.WriteLine(
        $"{Path.GetFileName(args[0])}: {header.Width} x {header.Height}, decoded {decodedMegabytes:F0} MB, "
            + $"peak above baseline {peakMegabytes:F0} MB, multiplier {peakMegabytes / decodedMegabytes:F2}, "
            + $"time {stopwatch.Elapsed.TotalSeconds:F1} s"
    );
    return 0;
}
finally
{
    root.Delete(recursive: true);
}

static string NewStorageKey() => Guid.CreateVersion7().ToString("n");

static long ReadStatusKilobytes(string field)
{
    // a line looks like "VmHWM:     123456 kB"
    var line = File.ReadLines("/proc/self/status").Single(line => line.StartsWith(field + ":"));
    return long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
}
