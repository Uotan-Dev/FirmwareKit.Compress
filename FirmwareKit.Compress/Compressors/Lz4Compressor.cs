using System.Buffers;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// LZ4 压缩/解压：标准帧（LZ4Stream）、传统块帧（magiskboot 风格）与 LG 块帧。
/// <para>LZ4 compression/decompression: standard frame (LZ4Stream), legacy block framing
/// (magiskboot style) and LG block framing.</para>
/// </summary>
public static class Lz4Compressor
{
    private const int BlockChunkSize = 0x10000;

    // Standard frame / 标准帧

    /// <summary>
    /// 标准 LZ4 帧压缩。
    /// <para>Standard LZ4 frame compression.</para>
    /// </summary>
    /// <param name="data">待压缩数据。<para>Data to compress.</para></param>
    /// <param name="options">压缩选项（Level：0-12，null=L00_FAST）。<para>Compression options (Level: 0-12, null=L00_FAST).</para></param>
    /// <returns>标准 LZ4 帧数据。<para>Standard LZ4 frame data.</para></returns>
    public static byte[] Compress(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            // 注：LZ4 不做分块并行。K4os LZ4Stream 解码器对 ≥512 KB 的串联帧不可靠
            // （只解首个帧），故保持单帧串行输出（与既有实现逐字节一致）。
            // Note: LZ4 is not chunked in parallel. The K4os LZ4Stream decoder does not
            // reliably decode concatenated frames ≥512 KB (only the first frame is read),
            // so single-frame sequential output is kept (byte-identical to before).
            using var output = new MemoryStream();
            using (var encoder = LZ4Stream.Encode(output, ToLz4Level(options?.Level), leaveOpen: true))
            {
                encoder.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4 压缩失败", ex);
        }
    }

    /// <summary>
    /// 标准 LZ4 帧解压。
    /// <para>Standard LZ4 frame decompression.</para>
    /// </summary>
    /// <param name="data">标准 LZ4 帧数据。<para>Standard LZ4 frame data.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var input = new MemoryStream(data);
            using var decoder = LZ4Stream.Decode(input);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4 解压失败", ex);
        }
    }

    /// <summary>
    /// 流式标准 LZ4 帧压缩。
    /// <para>Streaming standard LZ4 frame compression.</para>
    /// </summary>
    /// <param name="input">待压缩的输入流。<para>The input stream to compress.</para></param>
    /// <param name="output">写入压缩结果的输出流。<para>The output stream that receives the compressed data.</para></param>
    /// <param name="options">压缩选项（Level：0-12，null=L00_FAST）。<para>Compression options (Level: 0-12, null=L00_FAST).</para></param>
    public static void Compress(Stream input, Stream output, CompressionOptions? options = null)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        try
        {
            using var encoder = LZ4Stream.Encode(output, ToLz4Level(options?.Level), leaveOpen: true);
            input.CopyTo(encoder);
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4 压缩失败", ex);
        }
    }

    /// <summary>
    /// 流式标准 LZ4 帧解压。
    /// <para>Streaming standard LZ4 frame decompression.</para>
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
            using var decoder = LZ4Stream.Decode(input, leaveOpen: true);
            decoder.CopyTo(output);
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4 解压失败", ex);
        }
    }

    // Legacy block framing (magiskboot style) / 传统块帧

    /// <summary>
    /// LZ4 传统格式压缩（0x02 0x21 0x4C 0x18 魔数 + 4 字节块长前缀）。
    /// <para>LZ4 legacy compression (0x02 0x21 0x4C 0x18 magic + 4-byte block length prefixes).</para>
    /// </summary>
    public static byte[] CompressLegacy(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var output = new MemoryStream();
            output.WriteByte(0x02);
            output.WriteByte(0x21);
            output.WriteByte(0x4C);
            output.WriteByte(0x18);

            var level = ToLz4Level(options?.Level);
            int pos = 0;
            while (pos < data.Length)
            {
                int remaining = data.Length - pos;
                int chunk = remaining < BlockChunkSize ? remaining : BlockChunkSize;

                int maxSize = LZ4Codec.MaximumOutputSize(chunk);
                var compressed = new byte[maxSize];
                int size = LZ4Codec.Encode(data, pos, chunk, compressed, 0, maxSize, level);

                output.WriteByte((byte)(size & 0xFF));
                output.WriteByte((byte)((size >> 8) & 0xFF));
                output.WriteByte((byte)((size >> 16) & 0xFF));
                output.WriteByte((byte)((size >> 24) & 0xFF));
                output.Write(compressed, 0, size);

                pos += chunk;
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4_LEGACY 压缩失败", ex);
        }
    }

    /// <summary>
    /// LZ4 传统格式解压。
    /// <para>LZ4 legacy decompression.</para>
    /// </summary>
    public static byte[] DecompressLegacy(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            int offset = 0;
            if (data.Length >= 4 && data[0] == 0x02 && data[1] == 0x21 && data[2] == 0x4C && data[3] == 0x18)
                offset = 4;

            using var output = new MemoryStream();
            int pos = offset;
            byte[]? pooled = null;
            try
            {
                while (pos < data.Length)
                {
                    if (data.Length - pos < 4)
                        break;

                    int blockLength = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24);
                    pos += 4;

                    if (blockLength <= 0 || pos + blockLength > data.Length)
                        break;

                    // LZ4 块最大膨胀率 255:1；用该上界作为输出缓冲，跨块复用 ArrayPool 减少分配。
                    // LZ4 block expansion is capped at 255:1; reuse a pooled buffer across blocks.
                    int maxDecompressed = blockLength < (int.MaxValue / 255) ? blockLength * 255 : int.MaxValue;
                    if (pooled == null || pooled.Length < maxDecompressed)
                    {
                        if (pooled != null) ArrayPool<byte>.Shared.Return(pooled);
                        pooled = ArrayPool<byte>.Shared.Rent(maxDecompressed);
                    }
                    int decoded = LZ4Codec.Decode(data, pos, blockLength, pooled, 0, maxDecompressed);
                    if (decoded > 0)
                        output.Write(pooled, 0, decoded);

                    pos += blockLength;
                }
            }
            finally
            {
                if (pooled != null) ArrayPool<byte>.Shared.Return(pooled);
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4_LEGACY 解压失败", ex);
        }
    }

    // LG block framing / LG 块帧

    /// <summary>
    /// LZ4 LG 格式压缩（0x04 0x22 0x4D 0x40 魔数 + 4 字节块长前缀，仅用于 LG 设备）。
    /// <para>LZ4 LG compression (0x04 0x22 0x4D 0x40 magic + 4-byte block length prefixes, used only for LG devices).</para>
    /// </summary>
    public static byte[] CompressLg(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var output = new MemoryStream();
            output.WriteByte(0x04);
            output.WriteByte(0x22);
            output.WriteByte(0x4D);
            output.WriteByte(0x40);

            var level = ToLz4Level(options?.Level);
            int pos = 0;
            while (pos < data.Length)
            {
                int remaining = data.Length - pos;
                int chunk = remaining < BlockChunkSize ? remaining : BlockChunkSize;

                int maxSize = LZ4Codec.MaximumOutputSize(chunk);
                var compressed = new byte[maxSize];
                int size = LZ4Codec.Encode(data, pos, chunk, compressed, 0, maxSize, level);

                output.WriteByte((byte)(size & 0xFF));
                output.WriteByte((byte)((size >> 8) & 0xFF));
                output.WriteByte((byte)((size >> 16) & 0xFF));
                output.WriteByte((byte)((size >> 24) & 0xFF));
                output.Write(compressed, 0, size);

                pos += chunk;
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4_LG 压缩失败", ex);
        }
    }

    /// <summary>
    /// LZ4 LG 格式解压。
    /// <para>LZ4 LG decompression.</para>
    /// </summary>
    public static byte[] DecompressLg(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            int offset = 0;
            if (data.Length >= 4 && data[0] == 0x04 && data[1] == 0x22 && data[2] == 0x4D && data[3] == 0x40)
                offset = 4;

            using var output = new MemoryStream();
            int pos = offset;
            byte[]? pooled = null;
            try
            {
                while (pos < data.Length)
                {
                    if (data.Length - pos < 4)
                        break;

                    int blockLength = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24);
                    pos += 4;

                    if (blockLength <= 0 || pos + blockLength > data.Length)
                        break;

                    // LZ4 块最大膨胀率 255:1；用该上界作为输出缓冲，跨块复用 ArrayPool 减少分配。
                    // LZ4 block expansion is capped at 255:1; reuse a pooled buffer across blocks.
                    int maxDecompressed = blockLength < (int.MaxValue / 255) ? blockLength * 255 : int.MaxValue;
                    if (pooled == null || pooled.Length < maxDecompressed)
                    {
                        if (pooled != null) ArrayPool<byte>.Shared.Return(pooled);
                        pooled = ArrayPool<byte>.Shared.Rent(maxDecompressed);
                    }
                    int decoded = LZ4Codec.Decode(data, pos, blockLength, pooled, 0, maxDecompressed);
                    if (decoded > 0)
                        output.Write(pooled, 0, decoded);

                    pos += blockLength;
                }
            }
            finally
            {
                if (pooled != null) ArrayPool<byte>.Shared.Return(pooled);
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZ4_LG 解压失败", ex);
        }
    }

    // Detection / 格式检测

    /// <summary>
    /// 检测数据是否为标准 LZ4 帧（魔数 04 22 4D 18）。
    /// <para>Detects whether the data is a standard LZ4 frame (magic 04 22 4D 18).</para>
    /// </summary>
    public static bool IsLz4Format(byte[] data)
    {
        return data is { Length: >= 4 } &&
               data[0] == 0x04 && data[1] == 0x22 && data[2] == 0x4D && data[3] == 0x18;
    }

    /// <summary>
    /// 检测数据是否为 LZ4 传统格式（魔数 02 21 4C 18）。
    /// <para>Detects whether the data is in LZ4 legacy format (magic 02 21 4C 18).</para>
    /// </summary>
    public static bool IsLz4LegacyFormat(byte[] data)
    {
        return data is { Length: >= 4 } &&
               data[0] == 0x02 && data[1] == 0x21 && data[2] == 0x4C && data[3] == 0x18;
    }

    /// <summary>
    /// 检测数据是否为 LZ4 LG 格式（魔数 04 22 4D 40）。
    /// <para>Detects whether the data is in LZ4 LG format (magic 04 22 4D 40).</para>
    /// </summary>
    public static bool IsLz4LgFormat(byte[] data)
    {
        return data is { Length: >= 4 } &&
               data[0] == 0x04 && data[1] == 0x22 && data[2] == 0x4D && data[3] == 0x40;
    }

    private static LZ4Level ToLz4Level(int? level)
    {
        if (!level.HasValue)
            return LZ4Level.L00_FAST;
        int v = level.Value;
        return (LZ4Level)(v < 0 ? 0 : v > 12 ? 12 : v);
    }
}
