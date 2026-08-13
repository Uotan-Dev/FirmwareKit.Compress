using FirmwareKit.Compress.Compressors;

namespace FirmwareKit.Compress;

/// <summary>
/// 压缩选项。
/// <para>Compression options.</para>
/// <para>各格式按其自身语义解释这些选项；不支持的选项会被忽略。</para>
/// <para>Each format interprets these options with its own semantics; unsupported options are ignored.</para>
/// </summary>
public sealed class CompressionOptions
{
    /// <summary>
    /// 压缩级别。
    /// <para>Compression level.</para>
    /// <para>gzip/zlib/deflate/brotli/bzip2：0-9（0=不压缩，9=最高）；zstd：-5..22；lz4：0-12；null 表示使用默认级别。</para>
    /// <para>gzip/zlib/deflate/brotli/bzip2: 0-9 (0=stored, 9=best); zstd: -5..22; lz4: 0-12; null means the format default.</para>
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// 字典大小（字节），仅 lzma / xz 使用；null 表示使用格式默认值。
    /// <para>Dictionary size in bytes, used only by lzma / xz; null means the format default.</para>
    /// </summary>
    public uint? DictionarySize { get; set; }

    /// <summary>
    /// Zopfli 专用选项；仅当格式为 <see cref="CompressionFormat.Zopfli"/> 时使用。
    /// <para>Zopfli-specific options; used only when the format is <see cref="CompressionFormat.Zopfli"/>.</para>
    /// </summary>
    public ZopfliOptions? Zopfli { get; set; }
}
