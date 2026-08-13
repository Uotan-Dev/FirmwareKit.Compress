using System.IO.Compression;
using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 往返测试：所有格式 byte[] API 的压缩/解压一致性、互操作性与压缩比。
/// </summary>
public class RoundTripTests
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
    public void CompressDecompress_TextData_RoundTrips(CompressionFormat format)
    {
        var data = TestData.MakeText(4096);
        var compressed = CompressionService.Compress(data, format);
        var decompressed = CompressionService.Decompress(compressed, format);
        Assert.Equal(data, decompressed);
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void CompressDecompress_EmptyData_RoundTrips(CompressionFormat format)
    {
        var compressed = CompressionService.Compress(Array.Empty<byte>(), format);
        var decompressed = CompressionService.Decompress(compressed, format);
        Assert.Empty(decompressed);
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void CompressDecompress_RandomData_RoundTrips(CompressionFormat format)
    {
        var data = TestData.MakeRandom(2048, 7);
        var compressed = CompressionService.Compress(data, format);
        Assert.Equal(data, CompressionService.Decompress(compressed, format));
    }

    [Theory]
    [InlineData(CompressionFormat.Gzip, CompressionFormat.Gzip)]
    [InlineData(CompressionFormat.Zlib, CompressionFormat.Zlib)]
    [InlineData(CompressionFormat.Lz4, CompressionFormat.Lz4)]
    [InlineData(CompressionFormat.Lz4Legacy, CompressionFormat.Lz4Legacy)]
    [InlineData(CompressionFormat.Lz4Lg, CompressionFormat.Lz4Lg)]
    [InlineData(CompressionFormat.Lzma, CompressionFormat.Lzma)]
    [InlineData(CompressionFormat.Xz, CompressionFormat.Xz)]
    [InlineData(CompressionFormat.Bzip2, CompressionFormat.Bzip2)]
    [InlineData(CompressionFormat.Lzop, CompressionFormat.Lzop)]
    [InlineData(CompressionFormat.Zstd, CompressionFormat.Zstd)]
    public void Detect_AfterCompress_IdentifiesFormat(CompressionFormat format, CompressionFormat expected)
    {
        var compressed = CompressionService.Compress(TestData.MakeText(2048), format);
        Assert.Equal(expected, CompressionFormats.Detect(compressed));
    }

    [Theory]
    [InlineData(CompressionFormat.Deflate)]
    [InlineData(CompressionFormat.Brotli)]
    public void Detect_AfterCompress_UndetectableByMagic(CompressionFormat format)
    {
        var compressed = CompressionService.Compress(TestData.MakeText(2048), format);
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(compressed));
    }

    [Fact]
    public void Detect_ZopfliOutput_IsGzip()
    {
        var compressed = CompressionService.Compress(TestData.MakeText(2048), CompressionFormat.Zopfli);
        Assert.Equal(CompressionFormat.Gzip, CompressionFormats.Detect(compressed));
    }

    [Fact]
    public void GzipOutput_IsDecodableByDotNetGZipStream()
    {
        var data = TestData.MakeText(8192);
        var compressed = CompressionService.Compress(data, CompressionFormat.Gzip);
        Assert.Equal(data, DecodeWith(compressed, s => new GZipStream(s, CompressionMode.Decompress)));
    }

    [Fact]
    public void ZopfliOutput_IsDecodableByDotNetGZipStream()
    {
        var data = TestData.MakeText(4096);
        var compressed = CompressionService.Compress(data, CompressionFormat.Zopfli);
        Assert.Equal(data, DecodeWith(compressed, s => new GZipStream(s, CompressionMode.Decompress)));
    }

    [Fact]
    public void ZlibOutput_IsDecodableByDotNetZLibStream()
    {
        var data = TestData.MakeText(8192);
        var compressed = CompressionService.Compress(data, CompressionFormat.Zlib);
        Assert.Equal(data, DecodeWith(compressed, s => new ZLibStream(s, CompressionMode.Decompress)));
    }

    [Fact]
    public void DeflateOutput_IsDecodableByDotNetDeflateStream()
    {
        var data = TestData.MakeText(8192);
        var compressed = CompressionService.Compress(data, CompressionFormat.Deflate);
        Assert.Equal(data, DecodeWith(compressed, s => new DeflateStream(s, CompressionMode.Decompress)));
    }

    [Fact]
    public void RepetitiveData_CompressesWell_ForLosslessFormats()
    {
        var data = TestData.MakeRepetitive(64 * 1024);
        var expectations = new (CompressionFormat Format, double Ratio)[]
        {
            (CompressionFormat.Gzip, 0.2),
            (CompressionFormat.Bzip2, 0.2),
            (CompressionFormat.Lzma, 0.1),
            (CompressionFormat.Xz, 0.1),
            (CompressionFormat.Zstd, 0.1),
            (CompressionFormat.Lz4, 0.5),
        };

        foreach (var (format, ratio) in expectations)
        {
            var compressed = CompressionService.Compress(data, format);
            Assert.True(compressed.Length < data.Length * ratio,
                $"{format}: compressed={compressed.Length} original={data.Length}");
        }
    }

    [Fact]
    public void None_IsIdentityOperation()
    {
        var data = TestData.MakeRandom(100, 1);
        Assert.Equal(data, CompressionService.Compress(data, CompressionFormat.None));
        Assert.Equal(data, CompressionService.Decompress(data, CompressionFormat.None));
    }

    [Fact]
    public void LevelOption_ChangesOutputSize()
    {
        var data = TestData.MakeRepetitive(32 * 1024);
        var best = CompressionService.Compress(data, CompressionFormat.Gzip, new CompressionOptions { Level = 9 });
        var stored = CompressionService.Compress(data, CompressionFormat.Gzip, new CompressionOptions { Level = 0 });

        Assert.True(best.Length < stored.Length, $"best={best.Length} stored={stored.Length}");
        Assert.Equal(data, CompressionService.Decompress(best, CompressionFormat.Gzip));
        Assert.Equal(data, CompressionService.Decompress(stored, CompressionFormat.Gzip));
    }

    [Fact]
    public void Zstd_NegativeLevel_RoundTrips()
    {
        var data = TestData.MakeText(8192);
        var compressed = CompressionService.Compress(data, CompressionFormat.Zstd, new CompressionOptions { Level = -5 });
        Assert.Equal(data, CompressionService.Decompress(compressed, CompressionFormat.Zstd));
    }

    [Fact]
    public void Xz_DictionarySizeOption_RoundTrips()
    {
        var data = TestData.MakeRepetitive(128 * 1024);
        var compressed = CompressionService.Compress(data, CompressionFormat.Xz, new CompressionOptions { DictionarySize = 1u << 20 });
        Assert.Equal(data, CompressionService.Decompress(compressed, CompressionFormat.Xz));
    }

    [Fact]
    public void Xz_Crc64CheckType_RoundTrips()
    {
        // 走自研 Crc64（XZ 变体）路径：SharpCompress 解压时会验证 CRC64 校验值。
        var data = TestData.MakeText(64 * 1024);
        byte[] compressed = Compressors.XzCompressor.Compress(data, 1u << 23, checkType: 0x04); // CRC64
        Assert.Equal(data, CompressionService.Decompress(compressed, CompressionFormat.Xz));
    }

    [Fact]
    public void Xz_Crc64Empty_RoundTrips()
    {
        byte[] compressed = Compressors.XzCompressor.Compress(Array.Empty<byte>(), 1u << 23, checkType: 0x04);
        Assert.Empty(CompressionService.Decompress(compressed, CompressionFormat.Xz));
    }

    [Fact]
    public void Zopfli_Options_RoundTrip()
    {
        var data = TestData.MakeText(2048);
        var options = new CompressionOptions
        {
            Zopfli = new Compressors.ZopfliOptions { NumIterations = 5, BlockSplitting = false }
        };
        var compressed = CompressionService.Compress(data, CompressionFormat.Zopfli, options);
        Assert.Equal(data, CompressionService.Decompress(compressed, CompressionFormat.Zopfli));
    }

    [Fact]
    public void Gzip_LargeData_RoundTrips()
    {
        var data = TestData.MakeText(1024 * 1024);
        var compressed = CompressionService.Compress(data, CompressionFormat.Gzip);
        Assert.Equal(data, CompressionService.Decompress(compressed, CompressionFormat.Gzip));
    }

    [Fact]
    public void Lzma_LargeData_RoundTrips()
    {
        var data = TestData.MakeRepetitive(512 * 1024);
        var compressed = CompressionService.Compress(data, CompressionFormat.Lzma);
        Assert.Equal(data, CompressionService.Decompress(compressed, CompressionFormat.Lzma));
    }

    private static byte[] DecodeWith(byte[] compressed, Func<Stream, Stream> wrap)
    {
        using var input = new MemoryStream(compressed);
        using var decoder = wrap(input);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        return output.ToArray();
    }
}

internal static class TestData
{
    public static byte[] MakeText(int size)
    {
        var data = new byte[size];
        for (int i = 0; i < size; i++)
            data[i] = (byte)('a' + (i * 7) % 26);
        return data;
    }

    public static byte[] MakeRepetitive(int size)
    {
        var data = new byte[size];
        for (int i = 0; i < size; i++)
            data[i] = (byte)(i % 251);
        return data;
    }

    public static byte[] MakeRandom(int size, int seed)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        return data;
    }
}
