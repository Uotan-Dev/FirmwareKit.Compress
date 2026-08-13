using System.IO.Compression;

namespace FirmwareKit.Compress.Internal;

/// <summary>
/// 压缩级别映射助手：把统一的整数级别（0-9）映射到各库的级别枚举。
/// <para>Compression level mapping helper: maps the unified integer level (0-9) to each library's level enum.</para>
/// </summary>
internal static class CompressionLevelMapper
{
    /// <summary>
    /// Maps the unified integer level (0-9) to a Brotli quality value (0-11).
    /// <para>将统一整数级别（0-9）映射为 Brotli 质量值（0-11）。</para>
    /// <para>Used by the netstandard2.0 BrotliSharpLib backend, which takes a numeric quality
    /// instead of <see cref="CompressionLevel"/>.</para>
    /// </summary>
    public static int ToBrotliQuality(int? level)
    {
        if (!level.HasValue)
            return 4; // Optimal

        return level.Value switch
        {
            <= 0 => 0,  // NoCompression
            <= 3 => 1,  // Fastest
            >= 9 => 11, // SmallestSize
            _ => 5      // Optimal
        };
    }

    /// <summary>
    /// 映射到 SharpCompress 的 <see cref="SharpCompress.Compressors.Deflate.CompressionLevel"/>（zlib）。
    /// <para>Maps to SharpCompress's <see cref="SharpCompress.Compressors.Deflate.CompressionLevel"/> (zlib).</para>
    /// </summary>
    public static SharpCompress.Compressors.Deflate.CompressionLevel ToSharpCompressDeflate(int? level)
    {
        return level switch
        {
            null => SharpCompress.Compressors.Deflate.CompressionLevel.Default,
            <= 0 => SharpCompress.Compressors.Deflate.CompressionLevel.None,
            <= 3 => SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed,
            >= 9 => SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression,
            _ => SharpCompress.Compressors.Deflate.CompressionLevel.Default
        };
    }
}
