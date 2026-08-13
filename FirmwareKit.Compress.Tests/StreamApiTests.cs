using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 流式 API 测试：流式格式直接管道、块格式内存缓冲，以及 None 透传。
/// </summary>
public class StreamApiTests
{
    public static readonly TheoryData<CompressionFormat> AllFormats = new()
    {
        CompressionFormat.Gzip, CompressionFormat.Zlib, CompressionFormat.Deflate,
        CompressionFormat.Brotli, CompressionFormat.Lz4, CompressionFormat.Lz4Legacy,
        CompressionFormat.Lz4Lg, CompressionFormat.Lzma, CompressionFormat.Xz,
        CompressionFormat.Bzip2, CompressionFormat.Zopfli, CompressionFormat.Lzop,
        CompressionFormat.Zstd
    };

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void CompressDecompress_Streams_RoundTrip(CompressionFormat format)
    {
        var data = TestData.MakeText(4096);
        using var input = new MemoryStream(data);

        using var compressedStream = new MemoryStream();
        CompressionService.Compress(input, compressedStream, format);
        compressedStream.Position = 0;

        using var output = new MemoryStream();
        CompressionService.Decompress(compressedStream, output, format);

        Assert.Equal(data, output.ToArray());
    }

    [Fact]
    public void Compress_Streams_LargeInput_RoundTrips()
    {
        var format = CompressionFormat.Gzip;
        var data = TestData.MakeRepetitive(512 * 1024);
        using var input = new MemoryStream(data);

        using var compressedStream = new MemoryStream();
        CompressionService.Compress(input, compressedStream, format);
        compressedStream.Position = 0;

        using var output = new MemoryStream();
        CompressionService.Decompress(compressedStream, output, format);

        Assert.Equal(data, output.ToArray());
    }

    [Fact]
    public void None_PassesStreamThrough()
    {
        var data = TestData.MakeRandom(256, 3);
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        CompressionService.Compress(input, output, CompressionFormat.None);
        Assert.Equal(data, output.ToArray());
    }

    [Fact]
    public void BlockFormats_CompressThroughStream_AreDetectable()
    {
        var data = TestData.MakeText(4096);
        foreach (var format in new[] { CompressionFormat.Xz, CompressionFormat.Zstd, CompressionFormat.Lzma })
        {
            using var input = new MemoryStream(data);
            using var compressedStream = new MemoryStream();
            CompressionService.Compress(input, compressedStream, format);
            Assert.Equal(format, CompressionFormats.Detect(compressedStream.ToArray()));
        }
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void Compress_NullStreams_Throw(CompressionFormat format)
    {
        Assert.Throws<ArgumentNullException>(() => CompressionService.Compress(null!, new MemoryStream(), format));
        Assert.Throws<ArgumentNullException>(() => CompressionService.Compress(new MemoryStream(), null!, format));
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void Decompress_NullStreams_Throw(CompressionFormat format)
    {
        Assert.Throws<ArgumentNullException>(() => CompressionService.Decompress(null!, new MemoryStream(), format));
        Assert.Throws<ArgumentNullException>(() => CompressionService.Decompress(new MemoryStream(), null!, format));
    }

    [Fact]
    public void StreamCompression_HonorsLevelOption()
    {
        var data = TestData.MakeRepetitive(32 * 1024);
        using var input = new MemoryStream(data);

        using var best = new MemoryStream();
        CompressionService.Compress(input, best, CompressionFormat.Gzip, new CompressionOptions { Level = 9 });

        input.Position = 0;
        using var stored = new MemoryStream();
        CompressionService.Compress(input, stored, CompressionFormat.Gzip, new CompressionOptions { Level = 0 });

        Assert.True(best.Length < stored.Length, $"best={best.Length} stored={stored.Length}");
    }
}
