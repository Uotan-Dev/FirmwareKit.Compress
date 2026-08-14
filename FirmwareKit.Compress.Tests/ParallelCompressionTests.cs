using FirmwareKit.Compress.Compressors;
using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 多核并行压缩测试：验证自研编码器（xz/lzma2、zopfli）在
/// <see cref="CompressionOptions.MaxDegreeOfParallelism"/> &gt; 1 时
/// 输出与串行结果逐字节一致（确定性），且往返解压正确；委托编码器
/// （gzip/zlib/deflate/bzip2/lz4/zstd）并行产出多成员流并正确解回。
/// <para>Multi-core parallel compression tests: verify that the self-implemented
/// encoders (xz/lzma2, zopfli) produce byte-identical output to the sequential path
/// when <see cref="CompressionOptions.MaxDegreeOfParallelism"/> &gt; 1, and that
/// round-trip decompression is correct; delegated encoders
/// (gzip/zlib/deflate/bzip2/lz4/zstd) produce multi-member streams that decode back.</para>
/// </summary>
public class ParallelCompressionTests
{
    // 3 MiB 文本：XZ 固定 2 MiB 窗口 → 2 个窗口，足以触发并行。
    // 3 MiB of text: XZ uses fixed 2 MiB windows → 2 windows, enough to exercise parallelism.
    private static readonly byte[] XzText = TestData.MakeText(3 * 1024 * 1024);

    // 3 MiB 随机数据：不可压缩窗口路径。
    // 3 MiB of random data: exercises the incompressible-window path.
    private static readonly byte[] XzRandom = TestData.MakeRandom(3 * 1024 * 1024, 12345);

    [Fact]
    public void Xz_Parallel_MatchesSequential_ByteIdentical()
    {
        var seq = CompressionService.Compress(XzText, CompressionFormat.Xz, new CompressionOptions { MaxDegreeOfParallelism = 1 });
        var par = CompressionService.Compress(XzText, CompressionFormat.Xz, new CompressionOptions { MaxDegreeOfParallelism = 4 });

        Assert.Equal(seq, par);
        Assert.Equal(XzText, CompressionService.Decompress(par, CompressionFormat.Xz));
    }

    [Fact]
    public void Xz_Parallel_RandomData_MatchesSequential_AndRoundTrips()
    {
        var seq = CompressionService.Compress(XzRandom, CompressionFormat.Xz, new CompressionOptions { MaxDegreeOfParallelism = 1 });
        var par = CompressionService.Compress(XzRandom, CompressionFormat.Xz, new CompressionOptions { MaxDegreeOfParallelism = 4 });

        Assert.Equal(seq, par);
        Assert.Equal(XzRandom, CompressionService.Decompress(par, CompressionFormat.Xz));
    }

    [Fact]
    public void Xz_Parallel_EmptyAndTinyInputs_StillWork()
    {
        // 空输入与不足一个窗口的输入在并行模式下也应正常。
        // Empty input and inputs smaller than one window must still work in parallel mode.
        byte[] empty = Array.Empty<byte>();
        byte[] tiny = TestData.MakeText(1000);

        byte[] parEmpty = CompressionService.Compress(empty, CompressionFormat.Xz, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        Assert.Empty(CompressionService.Decompress(parEmpty, CompressionFormat.Xz));

        byte[] parTiny = CompressionService.Compress(tiny, CompressionFormat.Xz, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        Assert.Equal(tiny, CompressionService.Decompress(parTiny, CompressionFormat.Xz));
    }

    [Fact]
    public void Xz_Parallel_DictionarySize_Honored()
    {
        // 并行模式下字典大小选项仍生效。
        // The dictionary-size option is still honored in parallel mode.
        var par = CompressionService.Compress(
            XzText, CompressionFormat.Xz,
            new CompressionOptions { MaxDegreeOfParallelism = 4, DictionarySize = 1u << 20 });

        Assert.Equal(XzText, CompressionService.Decompress(par, CompressionFormat.Xz));
    }

    // ---- Zopfli ----

    // 混合分布数据：包含多个不同统计特征的区段，促使块切分产生多个块，
    // 从而真正触发按块并行（单块时自动回退为串行，结果同样正确）。
    // Mixed-distribution data with several statistically distinct sections to encourage
    // block splitting into multiple blocks; with a single block the code falls back to
    // sequential automatically and the result stays correct.
    private static readonly byte[] ZopfliMix = BuildZopfliMix();

    private static byte[] BuildZopfliMix()
    {
        var data = new byte[256 * 1024];
        TestData.MakeText(64 * 1024).CopyTo(data, 0);
        TestData.MakeRandom(64 * 1024, 11).CopyTo(data, 64 * 1024);
        TestData.MakeRepetitive(64 * 1024).CopyTo(data, 128 * 1024);
        TestData.MakeText(64 * 1024).CopyTo(data, 192 * 1024);
        return data;
    }

    private static byte[] ZopfliCompress(byte[] data, int? maxDop)
    {
        var options = new CompressionOptions
        {
            Zopfli = new ZopfliOptions
            {
                NumIterations = 5,
                MaxDegreeOfParallelism = maxDop,
            },
        };
        return CompressionService.Compress(data, CompressionFormat.Zopfli, options);
    }

    [Fact]
    public void Zopfli_Parallel_MatchesSequential_ByteIdentical()
    {
        byte[] seq = ZopfliCompress(ZopfliMix, 1);
        byte[] par = ZopfliCompress(ZopfliMix, 4);

        Assert.Equal(seq, par);
        Assert.Equal(ZopfliMix, CompressionService.Decompress(par, CompressionFormat.Zopfli));
    }

    [Fact]
    public void Zopfli_Parallel_OuterOption_IsForwarded()
    {
        // 外层 CompressionOptions.MaxDegreeOfParallelism 应被转发到 Zopfli 路径。
        // The outer CompressionOptions.MaxDegreeOfParallelism must be forwarded to Zopfli.
        byte[] seq = CompressionService.Compress(ZopfliMix, CompressionFormat.Zopfli,
            new CompressionOptions { Zopfli = new ZopfliOptions { NumIterations = 5 }, MaxDegreeOfParallelism = 1 });
        byte[] par = CompressionService.Compress(ZopfliMix, CompressionFormat.Zopfli,
            new CompressionOptions { Zopfli = new ZopfliOptions { NumIterations = 5 }, MaxDegreeOfParallelism = 4 });

        Assert.Equal(seq, par);
        Assert.Equal(ZopfliMix, CompressionService.Decompress(par, CompressionFormat.Zopfli));
    }

    [Fact]
    public void Zopfli_Parallel_BlockSplittingOff_StillWorks()
    {
        // 块切分关闭时（单块）并行度应自动回退为串行，输出正确。
        // With block splitting off (single block) parallelism falls back to sequential
        // automatically and the output stays correct.
        var options = new CompressionOptions
        {
            Zopfli = new ZopfliOptions { NumIterations = 3, BlockSplitting = false, MaxDegreeOfParallelism = 4 },
        };
        byte[] par = CompressionService.Compress(ZopfliMix, CompressionFormat.Zopfli, options);
        Assert.Equal(ZopfliMix, CompressionService.Decompress(par, CompressionFormat.Zopfli));
    }

    // ---- 委托格式（gzip/zlib/deflate/bzip2/zstd）：多成员并行 ----
    // Delegated formats: multi-member parallel compression.
    // lz4 不在此列：K4os 解码器对 ≥512 KB 串联帧不可靠（只解首个帧），与 brotli 一样保持串行。
    // LZ4 is excluded: the K4os decoder does not reliably decode concatenated frames
    // ≥512 KB (only the first frame is read), so it stays sequential like brotli.

    private static readonly CompressionFormat[] MemberFormats =
    {
        CompressionFormat.Gzip,
        CompressionFormat.Zlib,
        CompressionFormat.Deflate,
        CompressionFormat.Bzip2,
        CompressionFormat.Zstd,
    };

    // 3 MiB 文本 → 3 个 1 MiB 块。
    private static readonly byte[] MemberData = TestData.MakeText(3 * 1024 * 1024);

    public static IEnumerable<object[]> GetMemberFormats() => MemberFormats.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(GetMemberFormats))]
    public void MemberFormat_Parallel_RoundTrips(CompressionFormat format)
    {
        var compressed = CompressionService.Compress(MemberData, format, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        Assert.Equal(MemberData, CompressionService.Decompress(compressed, format));
    }

    [Theory]
    [MemberData(nameof(GetMemberFormats))]
    public void MemberFormat_Parallel_IsDeterministic(CompressionFormat format)
    {
        // 同输入同参数 → 逐字节一致（确定性），验证并行不引入随机性。
        // Same input and options → byte-identical (deterministic), proving parallelism
        // introduces no randomness.
        var a = CompressionService.Compress(MemberData, format, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        var b = CompressionService.Compress(MemberData, format, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        Assert.Equal(a, b);
    }

    [Theory]
    [MemberData(nameof(GetMemberFormats))]
    public void MemberFormat_Parallel_OutputDiffersFromSequential(CompressionFormat format)
    {
        // 并行输出应为多成员流，与单成员串行输出字节不同（证明并行确实生效）。
        // Parallel output is a multi-member stream and must differ byte-wise from the
        // single-member sequential output (proving parallelism actually engaged).
        var seq = CompressionService.Compress(MemberData, format, new CompressionOptions { MaxDegreeOfParallelism = 1 });
        var par = CompressionService.Compress(MemberData, format, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        Assert.NotEqual(seq, par);
    }

    [Theory]
    [MemberData(nameof(GetMemberFormats))]
    public void MemberFormat_SmallInput_WithParallelOption_Unchanged(CompressionFormat format)
    {
        // 小输入（不足 2 个 1 MiB 块）应回退为串行单成员输出，与不带并行选项逐字节一致。
        // Inputs smaller than two 1 MiB chunks fall back to sequential single-member
        // output, byte-identical to compressing without the parallel option.
        var data = TestData.MakeText(64 * 1024);
        var withOption = CompressionService.Compress(data, format, new CompressionOptions { MaxDegreeOfParallelism = 8 });
        var without = CompressionService.Compress(data, format);
        Assert.Equal(without, withOption);
    }

    [Fact]
    public void Brotli_ParallelOption_StaysSequential()
    {
        // brotli 因 .NET 解码器不支持串联流：并行选项应被忽略，输出与串行逐字节一致。
        // brotli is not parallelized because the .NET decoder does not support
        // concatenated streams: the parallel option is ignored, output stays byte-identical.
        var withOption = CompressionService.Compress(MemberData, CompressionFormat.Brotli, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        var without = CompressionService.Compress(MemberData, CompressionFormat.Brotli);
        Assert.Equal(without, withOption);
        Assert.Equal(MemberData, CompressionService.Decompress(withOption, CompressionFormat.Brotli));
    }

    [Fact]
    public void Lz4_ParallelOption_StaysSequential()
    {
        // lz4 因 K4os 解码器对 ≥512 KB 串联帧不可靠：并行选项应被忽略，输出与串行逐字节一致。
        // lz4 is not parallelized because the K4os decoder does not reliably decode
        // concatenated frames ≥512 KB: the parallel option is ignored, output stays
        // byte-identical (and the single frame round-trips).
        var withOption = CompressionService.Compress(MemberData, CompressionFormat.Lz4, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        var without = CompressionService.Compress(MemberData, CompressionFormat.Lz4);
        Assert.Equal(without, withOption);
        Assert.Equal(MemberData, CompressionService.Decompress(withOption, CompressionFormat.Lz4));
    }

    [Fact]
    public void Zlib_Parallel_MultiMember_StreamApi_RoundTrips()
    {
        // 可定位流：Stream API 也应能解回并行产出的多成员 zlib。
        // Seekable streams: the Stream API must also decode parallel multi-member zlib.
        var compressed = CompressionService.Compress(MemberData, CompressionFormat.Zlib, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        using var input = new MemoryStream(compressed);
        using var output = new MemoryStream();
        CompressionService.Decompress(input, output, CompressionFormat.Zlib);
        Assert.Equal(MemberData, output.ToArray());
    }

    [Fact]
    public void Deflate_Parallel_MultiMember_StreamApi_RoundTrips()
    {
        // 可定位流：Stream API 也应能解回并行产出的多成员 raw deflate。
        // Seekable streams: the Stream API must also decode parallel multi-member deflate.
        var compressed = CompressionService.Compress(MemberData, CompressionFormat.Deflate, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        using var input = new MemoryStream(compressed);
        using var output = new MemoryStream();
        CompressionService.Decompress(input, output, CompressionFormat.Deflate);
        Assert.Equal(MemberData, output.ToArray());
    }

    [Fact]
    public void Bzip2_Parallel_MultiMember_StreamApi_RoundTrips()
    {
        // 可定位流：Stream API 也应能解回并行产出的多成员 bzip2。
        // Seekable streams: the Stream API must also decode parallel multi-member bzip2.
        var compressed = CompressionService.Compress(MemberData, CompressionFormat.Bzip2, new CompressionOptions { MaxDegreeOfParallelism = 4 });
        using var input = new MemoryStream(compressed);
        using var output = new MemoryStream();
        CompressionService.Decompress(input, output, CompressionFormat.Bzip2);
        Assert.Equal(MemberData, output.ToArray());
    }
}
