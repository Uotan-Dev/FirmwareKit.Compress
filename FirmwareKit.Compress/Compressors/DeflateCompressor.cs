using FirmwareKit.Compress.Internal;
using System.IO.Compression;

namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// 原始 DEFLATE 压缩/解压（基于 .NET 内置 DeflateStream，RFC 1951，无 zlib/gzip 包装头）。
/// <para>Raw DEFLATE compression/decompression (based on the .NET built-in DeflateStream, RFC 1951, without zlib/gzip wrapping).</para>
/// </summary>
public static class DeflateCompressor
{
    /// <summary>
    /// DEFLATE 压缩。
    /// <para>DEFLATE compression.</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="options">压缩选项（Level：0-9，null=Optimal）。<para>Compression options (Level: 0-9, null=Optimal).</para></param>
    /// <returns>原始 deflate 流。<para>Raw deflate stream.</para></returns>
    public static byte[] Compress(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, Polyfill.MapCompressionLevel(options?.Level), leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("DEFLATE 压缩失败", ex);
        }
    }

    /// <summary>
    /// DEFLATE 解压。
    /// <para>DEFLATE decompression.</para>
    /// </summary>
    /// <param name="data">原始 deflate 流。<para>Raw deflate stream.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var input = new MemoryStream(data);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("DEFLATE 解压失败", ex);
        }
    }

    /// <summary>
    /// 流式 DEFLATE 压缩。
    /// <para>Streaming DEFLATE compression.</para>
    /// </summary>
    /// <param name="input">待压缩的输入流。<para>The input stream to compress.</para></param>
    /// <param name="output">写入压缩结果的输出流。<para>The output stream that receives the compressed data.</para></param>
    /// <param name="options">压缩选项（Level：0-9，null=Optimal）。<para>Compression options (Level: 0-9, null=Optimal).</para></param>
    public static void Compress(Stream input, Stream output, CompressionOptions? options = null)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        try
        {
            using var deflate = new DeflateStream(output, Polyfill.MapCompressionLevel(options?.Level), leaveOpen: true);
            input.CopyTo(deflate);
        }
        catch (Exception ex)
        {
            throw new CompressionException("DEFLATE 压缩失败", ex);
        }
    }

    /// <summary>
    /// 流式 DEFLATE 解压。
    /// <para>Streaming DEFLATE decompression.</para>
    /// </summary>
    /// <param name="input">待解压的输入流。<para>The input stream to decompress.</para></param>
    /// <param name="output">写入解压结果的输出流。<para>The output stream that receives the decompressed data.</para></param>
    public static void Decompress(Stream input, Stream output)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        try
        {
            using var deflate = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
            deflate.CopyTo(output);
        }
        catch (Exception ex)
        {
            throw new CompressionException("DEFLATE 解压失败", ex);
        }
    }
}
