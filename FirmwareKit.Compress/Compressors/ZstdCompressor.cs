using ZstdSharp;

namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// Zstandard (zstd) 压缩/解压（基于 ZstdSharp.Port 的全托管 zstd 移植，Zstandard v1.5.x）。
/// <para>Zstandard (zstd) compression/decompression (based on the fully-managed ZstdSharp.Port
/// port of zstd, Zstandard v1.5.x).</para>
/// </summary>
public static class ZstdCompressor
{
    /// <summary>
    /// Zstd 压缩。
    /// <para>Zstd compression.</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="options">压缩选项（Level：-5..22，null=3）。<para>Compression options (Level: -5..22, null=3).</para></param>
    /// <returns>zstd 帧数据。<para>Zstd frame data.</para></returns>
    public static byte[] Compress(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var compressor = new Compressor(options?.Level ?? 3);
            return compressor.Wrap(data).ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("ZSTD 压缩失败", ex);
        }
    }

    /// <summary>
    /// Zstd 解压。
    /// <para>Zstd decompression.</para>
    /// </summary>
    /// <param name="data">zstd 帧数据。<para>Zstd frame data.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(data).ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("ZSTD 解压失败", ex);
        }
    }

    /// <summary>
    /// 检测数据是否为 zstd 格式（魔数 28 B5 2F FD）。
    /// <para>Detects whether the data is in zstd format (magic 28 B5 2F FD).</para>
    /// </summary>
    public static bool IsZstdFormat(byte[] data)
    {
        return data is { Length: >= 4 } &&
               data[0] == 0x28 && data[1] == 0xB5 && data[2] == 0x2F && data[3] == 0xFD;
    }
}
