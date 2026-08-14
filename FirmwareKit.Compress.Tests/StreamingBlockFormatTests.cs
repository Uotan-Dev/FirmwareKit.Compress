using FirmwareKit.Compress.Compressors;
using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 块格式流式 API 测试：验证 xz/lzma/zstd/lz4_legacy/lz4_lg/lzop 的
/// Stream 级压缩/解压（有界内存、不整块缓冲）在大输入下往返正确，
/// 且 xz 流式输出与 byte[] 路径逐字节一致。
/// <para>Block-format streaming API tests: verify that Stream-level compression and
/// decompression of xz/lzma/zstd/lz4_legacy/lz4_lg/lzop (bounded memory, no full
/// buffering) round-trip correctly on large inputs, and that xz streaming output is
/// byte-identical to the byte[] path.</para>
/// </summary>
public class StreamingBlockFormatTests
{
    private static readonly byte[] LargeText = TestData.MakeText(3 * 1024 * 1024 + 123);
    private static readonly byte[] LargeRandom = TestData.MakeRandom(3 * 1024 * 1024 + 77, 99);

    private static byte[] StreamRoundTrip(CompressionFormat format, byte[] data, CompressionOptions? options = null)
    {
        using var input = new MemoryStream(data);
        using var compressed = new MemoryStream();
        CompressionService.Compress(input, compressed, format, options);

        compressed.Position = 0;
        using var output = new MemoryStream();
        CompressionService.Decompress(compressed, output, format);
        return output.ToArray();
    }

    public static IEnumerable<object[]> GetBlockFormats() => new[]
    {
        new object[] { CompressionFormat.Xz },
        new object[] { CompressionFormat.Lzma },
        new object[] { CompressionFormat.Zstd },
        new object[] { CompressionFormat.Lz4Legacy },
        new object[] { CompressionFormat.Lz4Lg },
        new object[] { CompressionFormat.Lzop },
    };

    [Theory]
    [MemberData(nameof(GetBlockFormats))]
    public void BlockFormat_StreamApi_LargeText_RoundTrips(CompressionFormat format)
    {
        Assert.Equal(LargeText, StreamRoundTrip(format, LargeText));
    }

    [Theory]
    [MemberData(nameof(GetBlockFormats))]
    public void BlockFormat_StreamApi_LargeRandom_RoundTrips(CompressionFormat format)
    {
        Assert.Equal(LargeRandom, StreamRoundTrip(format, LargeRandom));
    }

    [Fact]
    public void Xz_StreamApi_MatchesByteArray_ByteIdentical()
    {
        // 流式 xz 压缩必须与 byte[] 路径逐字节一致（确定性）。
        byte[] fromBytes = CompressionService.Compress(LargeText, CompressionFormat.Xz);

        using var input = new MemoryStream(LargeText);
        using var compressed = new MemoryStream();
        CompressionService.Compress(input, compressed, CompressionFormat.Xz);
        Assert.Equal(fromBytes, compressed.ToArray());
    }

    [Fact]
    public void Xz_StreamApi_WithParallelAndDict_Works()
    {
        var options = new CompressionOptions { MaxDegreeOfParallelism = 4, DictionarySize = 1u << 20 };
        Assert.Equal(LargeText, StreamRoundTrip(CompressionFormat.Xz, LargeText, options));
    }

    [Fact]
    public void Lzma_StreamApi_NonSeekableInput_RoundTrips()
    {
        // 不可定位输入 → 头部写未知大小（0xFFFFFFFFFFFFFFFF），解码端按 EndMarker 流式解出。
        using var inner = new MemoryStream(LargeText);
        using var input = new NonSeekableStream(inner);
        using var compressed = new MemoryStream();
        CompressionService.Compress(input, compressed, CompressionFormat.Lzma);

        compressed.Position = 0;
        using var output = new MemoryStream();
        CompressionService.Decompress(compressed, output, CompressionFormat.Lzma);
        Assert.Equal(LargeText, output.ToArray());
    }

    [Fact]
    public void Zopfli_StreamApi_StillBuffersAndRoundTrips()
    {
        // zopfli 需要整块输入（全局优化）：Stream API 内部缓冲，但往返仍正确。
        Assert.Equal(LargeText, StreamRoundTrip(CompressionFormat.Zopfli, LargeText));
    }

    [Theory]
    [MemberData(nameof(GetBlockFormats))]
    public void BlockFormat_StreamApi_EmptyInput_RoundTrips(CompressionFormat format)
    {
        Assert.Empty(StreamRoundTrip(format, Array.Empty<byte>()));
    }
}
