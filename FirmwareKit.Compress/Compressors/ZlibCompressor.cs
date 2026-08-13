using FirmwareKit.Compress.Internal;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// ZLIB 压缩/解压（基于 SharpCompress 的全托管 ZlibStream，RFC 1950）。
/// <para>ZLIB compression/decompression (based on SharpCompress's fully-managed ZlibStream, RFC 1950).</para>
/// </summary>
public static class ZlibCompressor
{
    /// <summary>
    /// ZLIB 压缩。
    /// <para>ZLIB compression.</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="options">压缩选项（Level：0-9，null=Default）。<para>Compression options (Level: 0-9, null=Default).</para></param>
    /// <returns>zlib 格式的压缩数据。<para>Compressed data in zlib format.</para></returns>
    public static byte[] Compress(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var output = new MemoryStream();
            using (var zlib = new ZlibStream(new NonDisposingStream(output), CompressionMode.Compress, CompressionLevelMapper.ToSharpCompressDeflate(options?.Level)))
            {
                zlib.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("ZLIB 压缩失败", ex);
        }
    }

    /// <summary>
    /// ZLIB 解压。
    /// <para>ZLIB decompression.</para>
    /// </summary>
    /// <param name="data">zlib 格式的压缩数据。<para>Compressed data in zlib format.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZlibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("ZLIB 解压失败", ex);
        }
    }

    /// <summary>
    /// 流式 ZLIB 压缩。
    /// <para>Streaming ZLIB compression.</para>
    /// </summary>
    public static void Compress(Stream input, Stream output, CompressionOptions? options = null)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        try
        {
            using var zlib = new ZlibStream(new NonDisposingStream(output), CompressionMode.Compress, CompressionLevelMapper.ToSharpCompressDeflate(options?.Level));
            input.CopyTo(zlib);
        }
        catch (Exception ex)
        {
            throw new CompressionException("ZLIB 压缩失败", ex);
        }
    }

    /// <summary>
    /// 流式 ZLIB 解压。
    /// <para>Streaming ZLIB decompression.</para>
    /// </summary>
    public static void Decompress(Stream input, Stream output)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        try
        {
            using var zlib = new ZlibStream(new NonDisposingStream(input), CompressionMode.Decompress);
            zlib.CopyTo(output);
        }
        catch (Exception ex)
        {
            throw new CompressionException("ZLIB 解压失败", ex);
        }
    }

    /// <summary>
    /// 检测数据是否为 zlib 格式（启发式：CM=8 且 CMF/FLG 可被 31 整除）。
    /// <para>Detects whether the data is in zlib format (heuristic: CM=8 and (CMF*256+FLG) % 31 == 0).</para>
    /// </summary>
    public static bool IsZlibFormat(byte[] data)
    {
        return data is { Length: >= 2 } &&
               (data[0] & 0x0F) == 8 &&
               (data[0] >> 4) <= 7 &&
               (((data[0] << 8) | data[1]) % 31) == 0;
    }
}
