using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 边界与错误处理测试：空参数、非法格式、损坏数据。
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Compress_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CompressionService.Compress(null!, CompressionFormat.Gzip));
    }

    [Fact]
    public void Decompress_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CompressionService.Decompress(null!, CompressionFormat.Gzip));
    }

    [Fact]
    public void Compress_InvalidFormat_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CompressionService.Compress(new byte[] { 1 }, (CompressionFormat)999));
    }

    [Fact]
    public void Decompress_InvalidFormat_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CompressionService.Decompress(new byte[] { 1 }, (CompressionFormat)999));
    }

    [Fact]
    public void Compress_None_DoesNotValidate()
    {
        var data = new byte[] { 1, 2, 3 };
        Assert.Equal(data, CompressionService.Compress(data, CompressionFormat.None));
    }

    [Fact]
    public void Decompress_CorruptedGzip_Throws()
    {
        var compressed = CompressionService.Compress(TestData.MakeText(1024), CompressionFormat.Gzip);
        compressed[10] ^= 0xFF;
        Assert.Throws<CompressionException>(() => CompressionService.Decompress(compressed, CompressionFormat.Gzip));
    }

    [Fact]
    public void Decompress_CorruptedZstd_Throws()
    {
        var compressed = CompressionService.Compress(TestData.MakeText(1024), CompressionFormat.Zstd);
        compressed[^1] ^= 0xFF;
        Assert.Throws<CompressionException>(() => CompressionService.Decompress(compressed, CompressionFormat.Zstd));
    }

    [Fact]
    public void Decompress_CorruptedBzip2_Throws()
    {
        var compressed = CompressionService.Compress(TestData.MakeText(1024), CompressionFormat.Bzip2);
        compressed[^1] ^= 0xFF;
        Assert.Throws<CompressionException>(() => CompressionService.Decompress(compressed, CompressionFormat.Bzip2));
    }

    [Fact]
    public void Decompress_CorruptedXz_Throws()
    {
        var compressed = CompressionService.Compress(TestData.MakeText(1024), CompressionFormat.Xz);
        compressed[^1] ^= 0xFF;
        Assert.Throws<CompressionException>(() => CompressionService.Decompress(compressed, CompressionFormat.Xz));
    }

    [Fact]
    public void Decompress_Lzop_WrongMagic_Throws()
    {
        Assert.Throws<CompressionException>(() =>
            CompressionService.Decompress(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, CompressionFormat.Lzop));
    }

    [Fact]
    public void Decompress_Lzma_TruncatedHeader_Throws()
    {
        Assert.Throws<CompressionException>(() =>
            CompressionService.Decompress(new byte[] { 0x5D, 0x00, 0x10 }, CompressionFormat.Lzma));
    }

    [Fact]
    public void Decompress_EmptyForNonNone_ThrowsOrEmpty_IsStable()
    {
        // 各格式对空输入的处理应保持一致：不崩溃且结果确定。
        foreach (var format in new[] { CompressionFormat.Gzip, CompressionFormat.Xz, CompressionFormat.Zstd })
        {
            var compressed = CompressionService.Compress(Array.Empty<byte>(), format);
            Assert.NotNull(compressed);
            Assert.Empty(CompressionService.Decompress(compressed, format));
        }
    }
}
