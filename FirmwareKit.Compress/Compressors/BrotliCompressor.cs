using FirmwareKit.Compress.Internal;

namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// Brotli 压缩/解压（RFC 7932）。
/// <para>Brotli compression/decompression (RFC 7932).</para>
/// <para>net8.0/net10.0 使用 .NET 内置 BrotliStream；netstandard2.0 使用纯托管的 BrotliSharpLib。
/// 两者差异由 <see cref="Polyfill"/> 统一。</para>
/// <para>Uses the built-in BrotliStream on net8.0/net10.0 and the pure-managed BrotliSharpLib
/// on netstandard2.0; both are unified by <see cref="Polyfill"/>.</para>
/// </summary>
public static class BrotliCompressor
{
    /// <summary>
    /// Brotli 压缩。
    /// <para>Brotli compression.</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="options">压缩选项（Level：0-9，null=Optimal）。<para>Compression options (Level: 0-9, null=Optimal).</para></param>
    /// <returns>Brotli 格式的压缩数据。<para>Compressed data in Brotli format.</para></returns>
    public static byte[] Compress(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var output = new MemoryStream();
            using (var brotli = Polyfill.CreateBrotliCompressor(output, options?.Level, leaveOpen: true))
            {
                brotli.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("BROTLI 压缩失败", ex);
        }
    }

    /// <summary>
    /// Brotli 解压。
    /// <para>Brotli decompression.</para>
    /// </summary>
    /// <param name="data">Brotli 格式的压缩数据。<para>Compressed data in Brotli format.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var input = new MemoryStream(data);
            using var brotli = Polyfill.CreateBrotliDecompressor(input, leaveOpen: false);
            using var output = new MemoryStream();
            brotli.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("BROTLI 解压失败", ex);
        }
    }

    /// <summary>
    /// 流式 Brotli 压缩。
    /// <para>Streaming Brotli compression.</para>
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
            using var brotli = Polyfill.CreateBrotliCompressor(output, options?.Level, leaveOpen: true);
            input.CopyTo(brotli);
        }
        catch (Exception ex)
        {
            throw new CompressionException("BROTLI 压缩失败", ex);
        }
    }

    /// <summary>
    /// 流式 Brotli 解压。
    /// <para>Streaming Brotli decompression.</para>
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
            using var brotli = Polyfill.CreateBrotliDecompressor(input, leaveOpen: true);
            brotli.CopyTo(output);
        }
        catch (Exception ex)
        {
            throw new CompressionException("BROTLI 解压失败", ex);
        }
    }
}
