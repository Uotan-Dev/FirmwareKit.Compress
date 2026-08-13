using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 格式注册表测试：枚举完整性、元数据、扩展名映射、名称解析。
/// </summary>
public class CompressionFormatsTests
{
    private static readonly CompressionFormat[] ExpectedOrder =
    {
        CompressionFormat.Gzip, CompressionFormat.Zlib, CompressionFormat.Deflate,
        CompressionFormat.Brotli, CompressionFormat.Lz4, CompressionFormat.Lz4Legacy,
        CompressionFormat.Lz4Lg, CompressionFormat.Lzma, CompressionFormat.Xz,
        CompressionFormat.Bzip2, CompressionFormat.Zopfli, CompressionFormat.Lzop,
        CompressionFormat.Zstd
    };

    [Fact]
    public void All_ContainsEverySupportedFormat()
    {
        Assert.Equal(ExpectedOrder, CompressionFormats.All.Select(f => f.Format));
        Assert.Equal(13, CompressionFormats.All.Count);
    }

    [Fact]
    public void All_DoesNotContainNone()
    {
        Assert.DoesNotContain(CompressionFormats.All, f => f.Format == CompressionFormat.None);
    }

    [Fact]
    public void GetInfo_ReturnsMetadataForEveryFormat()
    {
        foreach (var format in ExpectedOrder)
        {
            var info = CompressionFormats.GetInfo(format);
            Assert.NotNull(info);
            Assert.Equal(format, info!.Format);
            Assert.False(string.IsNullOrEmpty(info.Name));
            Assert.True(info.CanCompress);
            Assert.True(info.CanDecompress);
        }
    }

    [Fact]
    public void GetInfo_None_ReturnsNull()
    {
        Assert.Null(CompressionFormats.GetInfo(CompressionFormat.None));
    }

    [Fact]
    public void IsSupported_OnlyReturnsTrueForKnownFormats()
    {
        foreach (var format in ExpectedOrder)
            Assert.True(CompressionFormats.IsSupported(format));

        Assert.False(CompressionFormats.IsSupported(CompressionFormat.None));
        Assert.False(CompressionFormats.IsSupported((CompressionFormat)999));
    }

    [Theory]
    [InlineData(".gz", CompressionFormat.Gzip)]
    [InlineData(".gzip", CompressionFormat.Gzip)]
    [InlineData(".zlib", CompressionFormat.Zlib)]
    [InlineData(".deflate", CompressionFormat.Deflate)]
    [InlineData(".br", CompressionFormat.Brotli)]
    [InlineData(".lz4", CompressionFormat.Lz4)]
    [InlineData(".lz4_legacy", CompressionFormat.Lz4Legacy)]
    [InlineData(".lz4_lg", CompressionFormat.Lz4Lg)]
    [InlineData(".lzma", CompressionFormat.Lzma)]
    [InlineData(".xz", CompressionFormat.Xz)]
    [InlineData(".bz2", CompressionFormat.Bzip2)]
    [InlineData(".lzop", CompressionFormat.Lzop)]
    [InlineData(".zst", CompressionFormat.Zstd)]
    [InlineData(".unknown", CompressionFormat.None)]
    public void FromExtension_MapsKnownExtensions(string extension, CompressionFormat expected)
    {
        Assert.Equal(expected, CompressionFormats.FromExtension("boot.img" + extension));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("noextension")]
    public void FromExtension_NoExtension_ReturnsNone(string? filename)
    {
        Assert.Equal(CompressionFormat.None, CompressionFormats.FromExtension(filename!));
    }

    [Theory]
    [InlineData(CompressionFormat.Gzip, ".gz")]
    [InlineData(CompressionFormat.Zlib, ".zlib")]
    [InlineData(CompressionFormat.Deflate, ".deflate")]
    [InlineData(CompressionFormat.Brotli, ".br")]
    [InlineData(CompressionFormat.Lz4, ".lz4")]
    [InlineData(CompressionFormat.Lz4Legacy, ".lz4_legacy")]
    [InlineData(CompressionFormat.Lz4Lg, ".lz4_lg")]
    [InlineData(CompressionFormat.Lzma, ".lzma")]
    [InlineData(CompressionFormat.Xz, ".xz")]
    [InlineData(CompressionFormat.Bzip2, ".bz2")]
    [InlineData(CompressionFormat.Zopfli, ".gz")]
    [InlineData(CompressionFormat.Lzop, ".lzop")]
    [InlineData(CompressionFormat.Zstd, ".zst")]
    public void ToExtension_ReturnsPreferredExtension(CompressionFormat format, string expected)
    {
        Assert.Equal(expected, CompressionFormats.ToExtension(format));
    }

    [Fact]
    public void ToExtension_None_ReturnsEmpty()
    {
        Assert.Equal("", CompressionFormats.ToExtension(CompressionFormat.None));
    }

    [Fact]
    public void ExtensionRoundTrip_WorksForAllFormats()
    {
        foreach (var format in ExpectedOrder)
        {
            // Zopfli 与 gzip 共用 .gz 扩展名，无法反向唯一映射，单独断言。
            if (format == CompressionFormat.Zopfli)
            {
                Assert.Equal(CompressionFormat.Gzip, CompressionFormats.FromExtension("file" + CompressionFormats.ToExtension(format)));
                continue;
            }

            var ext = CompressionFormats.ToExtension(format);
            Assert.False(string.IsNullOrEmpty(ext));
            Assert.Equal(format, CompressionFormats.FromExtension("file" + ext));
        }
    }

    [Theory]
    [InlineData("gzip", CompressionFormat.Gzip)]
    [InlineData("GZIP", CompressionFormat.Gzip)]
    [InlineData("gz", CompressionFormat.Gzip)]
    [InlineData("zlib", CompressionFormat.Zlib)]
    [InlineData("deflate", CompressionFormat.Deflate)]
    [InlineData("def", CompressionFormat.Deflate)]
    [InlineData("brotli", CompressionFormat.Brotli)]
    [InlineData("br", CompressionFormat.Brotli)]
    [InlineData("lz4", CompressionFormat.Lz4)]
    [InlineData("lz4_legacy", CompressionFormat.Lz4Legacy)]
    [InlineData("lz4-legacy", CompressionFormat.Lz4Legacy)]
    [InlineData("lz4_lg", CompressionFormat.Lz4Lg)]
    [InlineData("lzma", CompressionFormat.Lzma)]
    [InlineData("xz", CompressionFormat.Xz)]
    [InlineData("bzip2", CompressionFormat.Bzip2)]
    [InlineData("bz2", CompressionFormat.Bzip2)]
    [InlineData("zopfli", CompressionFormat.Zopfli)]
    [InlineData("lzop", CompressionFormat.Lzop)]
    [InlineData("lzo", CompressionFormat.Lzop)]
    [InlineData("zstd", CompressionFormat.Zstd)]
    [InlineData("zst", CompressionFormat.Zstd)]
    public void Parse_AcceptsNamesAndAliases(string name, CompressionFormat expected)
    {
        Assert.Equal(expected, CompressionFormats.Parse(name));
    }

    [Fact]
    public void Parse_UnknownName_Throws()
    {
        Assert.Throws<ArgumentException>(() => CompressionFormats.Parse("unknown"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    [InlineData("gzip2")]
    public void TryParse_InvalidNames_ReturnsFalse(string? name)
    {
        Assert.False(CompressionFormats.TryParse(name, out var format));
        Assert.Equal(CompressionFormat.None, format);
    }

    [Fact]
    public void TryParse_ValidName_ReturnsTrue()
    {
        Assert.True(CompressionFormats.TryParse("XZ", out var format));
        Assert.Equal(CompressionFormat.Xz, format);
    }

    [Fact]
    public void GetDisplayName_MatchesRegistryNames()
    {
        Assert.Equal("gzip", CompressionFormats.GetDisplayName(CompressionFormat.Gzip));
        Assert.Equal("zopfli", CompressionFormats.GetDisplayName(CompressionFormat.Zopfli));
        Assert.Equal("none", CompressionFormats.GetDisplayName(CompressionFormat.None));
    }

    [Fact]
    public void MagicBytes_ArePresentForDetectableFormats()
    {
        var detectable = new[]
        {
            CompressionFormat.Gzip, CompressionFormat.Lz4, CompressionFormat.Lz4Legacy,
            CompressionFormat.Lz4Lg, CompressionFormat.Xz, CompressionFormat.Bzip2,
            CompressionFormat.Lzop, CompressionFormat.Zstd
        };

        foreach (var format in detectable)
            Assert.True(CompressionFormats.GetInfo(format)!.HasMagic, format.ToString());

        var undetectable = new[]
        {
            CompressionFormat.Zlib, CompressionFormat.Deflate, CompressionFormat.Brotli,
            CompressionFormat.Lzma, CompressionFormat.Zopfli
        };

        foreach (var format in undetectable)
            Assert.False(CompressionFormats.GetInfo(format)!.HasMagic, format.ToString());
    }

    [Fact]
    public void StreamingFlag_MatchesDesign()
    {
        var streaming = new[]
        {
            CompressionFormat.Gzip, CompressionFormat.Zlib, CompressionFormat.Deflate,
            CompressionFormat.Brotli, CompressionFormat.Bzip2, CompressionFormat.Lz4
        };

        foreach (var format in streaming)
            Assert.True(CompressionFormats.GetInfo(format)!.IsStreaming, format.ToString());

        var block = ExpectedOrder.Except(streaming);
        foreach (var format in block)
            Assert.False(CompressionFormats.GetInfo(format)!.IsStreaming, format.ToString());
    }
}
