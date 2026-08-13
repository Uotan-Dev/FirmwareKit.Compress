using FirmwareKit.Compress.Internal.Zopfli;

namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// Zopfli 压缩选项。
/// <para>Zopfli compression options.</para>
/// </summary>
public sealed class ZopfliOptions
{
    /// <summary>
    /// LZ77 优化迭代次数（默认 15）。值越大压缩率越高但越慢。
    /// <para>Number of LZ77 optimization iterations (default 15). Higher values give
    /// better compression at the cost of speed.</para>
    /// </summary>
    public int NumIterations { get; set; } = 15;

    /// <summary>
    /// 是否启用块切分（默认 true）。
    /// <para>Whether block splitting is enabled (default true).</para>
    /// </summary>
    public bool BlockSplitting { get; set; } = true;

    /// <summary>
    /// 最大切分块数（默认 15，0 表示不限）。
    /// <para>Maximum number of split blocks (default 15, 0 means unlimited).</para>
    /// </summary>
    public int BlockSplittingMax { get; set; } = 15;
}

/// <summary>
/// 真正的 Zopfli 压缩算法（纯 C# 移植，无 P/Invoke、无 unsafe、无外部进程）。
/// <para>The real Zopfli compression algorithm (pure C# port; no P/Invoke, no unsafe,
/// no external processes).</para>
/// <para>
/// Zopfli 是 Google 的高压缩率 deflate 编码器，压缩比高于 zlib 最佳级别，但速度慢
/// 约 100 倍。输出为标准的 gzip / zlib / deflate 流，可直接用任意 deflate 解码器解压。
/// 本实现基于 Tisit/zopfli-csharp（Apache-2.0）的纯 C# 移植。
/// </para>
/// <para>
/// Zopfli is Google's high-ratio deflate encoder; it compresses better than zlib's
/// best level but is roughly 100x slower. Output is a standard gzip / zlib / deflate
/// stream decodable by any deflate decoder. This implementation is based on the pure
/// C# port Tisit/zopfli-csharp (Apache-2.0).
/// </para>
/// </summary>
public static class ZopfliCompressor
{
    /// <summary>
    /// 以 gzip 格式压缩数据（与 magiskboot 的 zopfli 用法一致）。
    /// <para>Compresses data in gzip format (matches magiskboot's zopfli usage).</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="options">压缩选项；为 null 时使用默认值。<para>Compression options; defaults used when null.</para></param>
    /// <returns>gzip 格式的压缩数据。<para>Compressed data in gzip format.</para></returns>
    public static byte[] Compress(byte[] data, ZopfliOptions? options = null)
    {
        return Compress(data, ZopfliFormat.ZOPFLI_FORMAT_GZIP, options);
    }

    /// <summary>
    /// 按指定格式压缩数据。
    /// <para>Compresses data in the specified format.</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="format">输出格式：gzip / zlib / deflate。<para>Output format: gzip / zlib / deflate.</para></param>
    /// <param name="options">压缩选项；为 null 时使用默认值。<para>Compression options; defaults used when null.</para></param>
    /// <returns>指定格式的压缩数据。<para>Compressed data in the specified format.</para></returns>
    public static byte[] Compress(byte[] data, ZopfliFormat format, ZopfliOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        options ??= new ZopfliOptions();

        try
        {
            // 配置线程局部参数（与原版 CLI 行为一致）。
            Globals.output_type = format;
            Globals.numiterations = Math.Max(1, options.NumIterations);
            Globals.blocksplitting = options.BlockSplitting ? 1 : 0;
            Globals.blocksplittingmax = Math.Max(0, options.BlockSplittingMax);
            Globals.verbose = 0;
            Globals.verbose_more = 0;

            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            Internal.Zopfli.Compress.ZopfliCompress(input, output);
            return output.ToArray();
        }
        catch (CompressionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompressionException("Zopfli 压缩失败", ex);
        }
    }

    /// <summary>
    /// 解压 Zopfli 生成的 gzip 数据（Zopfli 只压缩；解压使用标准 gzip 解码）。
    /// <para>Decompresses gzip data produced by Zopfli (Zopfli only compresses;
    /// decompression uses the standard gzip decoder).</para>
    /// </summary>
    /// <param name="data">gzip 格式的压缩数据。<para>Compressed data in gzip format.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("Zopfli(gzip) 解压失败", ex);
        }
    }

    /// <summary>
    /// 检测数据是否为 gzip 格式（Zopfli gzip 输出同样适用）。
    /// <para>Detects whether the data is in gzip format (applies to Zopfli gzip output as well).</para>
    /// </summary>
    public static bool IsGzipFormat(byte[] data)
    {
        return data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;
    }
}
