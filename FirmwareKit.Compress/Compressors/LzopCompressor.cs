namespace FirmwareKit.Compress.Compressors;

/// <summary>
/// LZOP 压缩/解压（全托管实现）。
/// <para>LZOP compression/decompression (fully-managed implementation).</para>
/// <para>
/// 压缩使用存储模式（method=LZO1X-1，数据块原样存放），可生成标准 lzop 工具可读的 .lzo 文件；
/// 解压支持存储块与真正的 LZO1X 压缩块（内置 minilzo 风格的 <c>lzo1x_decompress_safe</c> 移植）。
/// </para>
/// <para>
/// Compression uses stored blocks (method=LZO1X-1, data stored verbatim), producing standard
/// .lzo files readable by the lzop tool; decompression supports both stored blocks and real
/// LZO1X-compressed blocks via an in-tree minilzo-style <c>lzo1x_decompress_safe</c> port.
/// </para>
/// <para>文件格式遵循 lzop 规范（大端序），魔数 89 4C 5A 4F 00 0D 0A 1A 0A。</para>
/// </summary>
public static class LzopCompressor
{
    private const int LzopMagicSize = 9;
    private static readonly byte[] LzopMagic = { 0x89, 0x4C, 0x5A, 0x4F, 0x00, 0x0D, 0x0A, 0x1A, 0x0A };

    // LZO methods (subset).
    private const byte M_LZO1X_1 = 1;

    // Header flags (lzop 1.0x semantics).
    // Empirical ground truth: real lzop file pg135.txt.lzo has flags=0x3 and a 16-byte
    // block header [u][c][u_csum][c_csum][data] with BOTH checksums (adler32). Hence
    // F_ADLER32_U=0x1 and F_ADLER32_C=0x2 (0x3 = U|C), and the CRC variants at 0x100/0x200.
    private const uint F_OS_MASK = 0xFF000000;
    private const uint F_OS_NT = 0x0B000000;
    private const uint F_H_FILTER = 0x00000800;
    private const uint F_H_CRC32 = 0x00001000;   // header checksum uses crc32 instead of adler32
    private const uint F_ADLER32_U = 0x00000001; // uncompressed data checksum is adler32
    private const uint F_ADLER32_C = 0x00000002; // compressed data checksum is adler32
    private const uint F_CRC32_U = 0x00000100;   // uncompressed data checksum is crc32
    private const uint F_CRC32_C = 0x00000200;   // compressed data checksum is crc32
    // Masks selecting the checksum algorithm for each payload.
    private const uint CsumUncompressedMask = F_ADLER32_U | F_CRC32_U;
    private const uint CsumCompressedMask = F_ADLER32_C | F_CRC32_C;

    /// <summary>
    /// 使用 LZOP 存储模式压缩数据。
    /// <para>Compresses data using LZOP stored mode.</para>
    /// </summary>
    /// <param name="data">待压缩的数据。<para>The data to compress.</para></param>
    /// <param name="options">压缩选项（存储模式下未使用）。<para>Compression options (unused in stored mode).</para></param>
    /// <returns>标准 .lzo 格式数据。<para>Standard .lzo data.</para></returns>
    public static byte[] Compress(byte[] data, CompressionOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        try
        {
            using var output = new MemoryStream();

            // ---- Magic ----
            output.Write(LzopMagic, 0, LzopMagicSize);

            // ---- Header fields (all big-endian) ----
            // version=0x1040, lib_version=0x1040, version_needed=0x0940
            WriteUInt16BE(output, 0x1040);
            WriteUInt16BE(output, 0x1040);
            WriteUInt16BE(output, 0x0940);
            output.WriteByte(M_LZO1X_1); // method
            output.WriteByte(1);         // level (stored)
            // flags: OS=NT + uncompressed-data adler32 checksum (lzop default).
            WriteUInt32BE(output, F_OS_NT | F_ADLER32_U);
            WriteUInt32BE(output, 0);      // mode
            WriteUInt32BE(output, 0);      // mtime_low
            WriteUInt32BE(output, 0);      // mtime_high

            output.WriteByte(0); // name length = 0 (no filename)

            // Header checksum (Adler32 over all header bytes after the magic).
            long headerStart = LzopMagicSize;
            long headerEnd = output.Position;
            byte[] headerBytes = output.ToArray();
            uint headerChecksum = Adler32(headerBytes, (int)headerStart, (int)(headerEnd - headerStart));
            WriteUInt32BE(output, headerChecksum);

            // ---- Data blocks ----
            // lzop writes blocks; for stored mode, compressed_size == uncompressed_size.
            // Block layout (matches real lzop and u-boot lzop_decompress):
            //   [uncompressed_size(4)][compressed_size(4)][uncompressed checksum(4)][data]
            int pos = 0;
            const int blockSize = 256 * 1024;
            while (pos < data.Length)
            {
                int chunk = Math.Min(blockSize, data.Length - pos);

                WriteUInt32BE(output, (uint)chunk);          // uncompressed size
                WriteUInt32BE(output, (uint)chunk);          // compressed size (stored)
                WriteUInt32BE(output, Adler32(data, pos, chunk)); // uncompressed checksum
                output.Write(data, pos, chunk);

                pos += chunk;
            }

            // ---- End marker: uncompressed_size = 0 ----
            WriteUInt32BE(output, 0);

            return output.ToArray();
        }
        catch (CompressionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompressionException("LZOP 压缩失败", ex);
        }
    }

    /// <summary>
    /// 解压 LZOP 格式数据。
    /// <para>Decompresses LZOP format data.</para>
    /// </summary>
    /// <param name="data">LZOP 格式数据。<para>LZOP format data.</para></param>
    /// <returns>解压后的原始数据。<para>The decompressed original data.</para></returns>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < LzopMagicSize)
            throw new CompressionException("Invalid LZOP data: too small");

        if (!IsLzopFormat(data))
            throw new CompressionException("Invalid LZOP data: wrong magic");

        try
        {
            int offset = LzopMagicSize;

            // Parse header.
            offset = ParseHeader(data, offset, out bool useCrc32, out bool hasUncompressedChecksum, out bool hasCompressedChecksum, out _);

            // Skip header checksum (4 bytes).
            offset += 4;

            using var output = new MemoryStream();

            // Parse blocks.
            while (offset + 4 <= data.Length)
            {
                uint uncompressedSize = ReadUInt32BE(data, ref offset);
                if (uncompressedSize == 0)
                    break; // end marker

                if (offset + 4 > data.Length)
                    throw new CompressionException("Invalid LZOP data: truncated block header");

                uint compressedSize = ReadUInt32BE(data, ref offset);

                // Uncompressed checksum (4 bytes) is present only when flags request it.
                uint uncompressedChecksum = 0;
                if (hasUncompressedChecksum)
                {
                    if (offset + 4 > data.Length)
                        throw new CompressionException("Invalid LZOP data: truncated uncompressed checksum");
                    uncompressedChecksum = ReadUInt32BE(data, ref offset);
                }

                // Compressed checksum (4 bytes) is present only when flags request it.
                if (hasCompressedChecksum)
                {
                    if (offset + 4 > data.Length)
                        throw new CompressionException("Invalid LZOP data: truncated compressed checksum");
                    offset += 4;
                }

                if (compressedSize > uncompressedSize && compressedSize > 0)
                {
                    // Shouldn't happen for LZO (compressed <= uncompressed), but guard.
                    throw new CompressionException("Invalid LZOP data: compressed block larger than uncompressed");
                }

                if (offset + (int)compressedSize > data.Length)
                    throw new CompressionException("Invalid LZOP data: truncated block data");

                byte[] blockOut = new byte[uncompressedSize];

                if (compressedSize == uncompressedSize)
                {
                    // Stored block.
                    Array.Copy(data, offset, blockOut, 0, (int)uncompressedSize);
                }
                else
                {
                    // LZO1X compressed block.
                    int decoded = Lzo1xDecompressSafe(data, offset, (int)compressedSize, blockOut, 0, (int)uncompressedSize);
                    if (decoded != (int)uncompressedSize)
                        throw new CompressionException($"Invalid LZOP data: block decompressed to {decoded} bytes, expected {uncompressedSize}");
                }

                // Verify uncompressed checksum when present.
                if (hasUncompressedChecksum)
                {
                    uint actualChecksum = useCrc32
                        ? Crc32Checksum(blockOut, 0, blockOut.Length)
                        : Adler32(blockOut, 0, blockOut.Length);
                    if (actualChecksum != uncompressedChecksum)
                    {
                        // Some lzop files use Adler32 even when F_H_CRC32 is not set; try the other.
                        uint otherChecksum = useCrc32
                            ? Adler32(blockOut, 0, blockOut.Length)
                            : Crc32Checksum(blockOut, 0, blockOut.Length);
                        if (otherChecksum != uncompressedChecksum)
                            throw new CompressionException("Invalid LZOP data: uncompressed checksum mismatch");
                    }
                }

                output.Write(blockOut, 0, blockOut.Length);
                offset += (int)compressedSize;
            }

            return output.ToArray();
        }
        catch (CompressionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompressionException($"LZOP decompression failed: {ex.Message}", ex);
        }
    }

    private static int ParseHeader(byte[] data, int offset, out bool useCrc32, out bool hasUncompressedChecksum, out bool hasCompressedChecksum, out byte method)
    {
        useCrc32 = false;
        hasUncompressedChecksum = false;
        hasCompressedChecksum = false;
        method = M_LZO1X_1;

        if (offset + 2 > data.Length)
            throw new CompressionException("Invalid LZOP header: truncated version");
        int version = ReadUInt16BE(data, ref offset);

        if (version < 0x0900)
            throw new CompressionException($"Invalid LZOP header: unsupported version {version:X4}");

        // lib_version (2 bytes)
        if (offset + 2 > data.Length) throw new CompressionException("Invalid LZOP header: truncated lib_version");
        offset += 2;

        // version_needed (2 bytes, present if version >= 0x0940)
        if (version >= 0x0940)
        {
            if (offset + 2 > data.Length) throw new CompressionException("Invalid LZOP header: truncated version_needed");
            offset += 2;
        }

        // method (1 byte)
        if (offset + 1 > data.Length) throw new CompressionException("Invalid LZOP header: truncated method");
        method = data[offset++];

        // level (1 byte, present if version >= 0x0940)
        if (version >= 0x0940)
        {
            if (offset + 1 > data.Length) throw new CompressionException("Invalid LZOP header: truncated level");
            offset++;
        }

        // flags (4 bytes)
        if (offset + 4 > data.Length) throw new CompressionException("Invalid LZOP header: truncated flags");
        uint flags = ReadUInt32BE(data, ref offset);
        useCrc32 = (flags & F_H_CRC32) != 0;
        hasUncompressedChecksum = (flags & (F_ADLER32_U | F_CRC32_U)) != 0;
        hasCompressedChecksum = (flags & (F_ADLER32_C | F_CRC32_C)) != 0;

        // filter (4 bytes, if F_H_FILTER)
        if ((flags & F_H_FILTER) != 0)
        {
            if (offset + 4 > data.Length) throw new CompressionException("Invalid LZOP header: truncated filter");
            offset += 4;
        }

        // mode (4 bytes)
        if (offset + 4 > data.Length) throw new CompressionException("Invalid LZOP header: truncated mode");
        offset += 4;

        // mtime_low (4 bytes)
        if (offset + 4 > data.Length) throw new CompressionException("Invalid LZOP header: truncated mtime_low");
        offset += 4;

        // mtime_high (4 bytes, if version >= 0x0940)
        if (version >= 0x0940)
        {
            if (offset + 4 > data.Length) throw new CompressionException("Invalid LZOP header: truncated mtime_high");
            offset += 4;
        }

        // filename length (1 byte) + filename
        if (offset + 1 > data.Length) throw new CompressionException("Invalid LZOP header: truncated name length");
        int nameLen = data[offset++];
        if (offset + nameLen > data.Length) throw new CompressionException("Invalid LZOP header: truncated name");
        offset += nameLen;

        return offset;
    }

    /// <summary>
    /// Checks if the data is in LZOP format (9-byte magic 89 4C 5A 4F 00 0D 0A 1A 0A).
    /// <para>检查数据是否为 LZOP 格式（9 字节魔数 89 4C 5A 4F 00 0D 0A 1A 0A）。</para>
    /// </summary>
    public static bool IsLzopFormat(byte[] data)
    {
        if (data == null || data.Length < LzopMagicSize)
            return false;

        for (int i = 0; i < LzopMagicSize; i++)
        {
            if (data[i] != LzopMagic[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Gets the LZOP compression method.
    /// <para>获取 LZOP 压缩方法。</para>
    /// </summary>
    public static int GetCompressionMethod(byte[] data)
    {
        if (!IsLzopFormat(data) || data.Length < LzopMagicSize + 10)
            return -1;

        int offset = LzopMagicSize;
        int version = (data[offset] << 8) | data[offset + 1];
        offset += 2 + 2; // version + lib_version
        if (version >= 0x0940) offset += 2; // version_needed
        return data[offset]; // method
    }

    /// <summary>
    /// Gets the LZOP version string.
    /// <para>获取 LZOP 版本字符串。</para>
    /// </summary>
    public static string GetVersion(byte[] data)
    {
        if (!IsLzopFormat(data) || data.Length < LzopMagicSize + 2)
            return "unknown";

        int ver = (data[LzopMagicSize] << 8) | data[LzopMagicSize + 1];
        return $"{ver >> 8}.{(ver >> 4) & 0xF}.{ver & 0xF}";
    }

    // ---- LZO1X decompressor (faithful port of minilzo lzo1x_decompress_safe) ----

    private const int M2_MAX_OFFSET = 0x0800;

    /// <summary>
    /// Safe LZO1X decompression. Ported from minilzo's lzo1x_decompress_safe with bounds checks.
    /// All labels are at method scope (C# restricts goto to same block scope).
    /// <para>安全的 LZO1X 解压，移植自 minilzo 的 lzo1x_decompress_safe，带边界检查。
    /// 所有标签位于方法作用域内。</para>
    /// </summary>
    private static int Lzo1xDecompressSafe(byte[] input, int inputStart, int inputLen, byte[] output, int outputStart, int outputLen)
    {
        int ip = inputStart;
        int ipEnd = inputStart + inputLen;
        int op = outputStart;
        int opEnd = outputStart + outputLen;
        int t;

        if (ip >= ipEnd)
            throw new CompressionException("LZO1X: empty input");

        // Peek at the first byte WITHOUT consuming it: when it is <= 17 it is the first
        // instruction of the main loop (minilzo reads it as t inside the while loop), so
        // MainLoop below must consume it. Only > 17 starts an initial literal run, in
        // which case we consume it here (t = first - 17 literals).
        byte first = input[ip];
        if (first > 17)
        {
            ip++;
            t = first - 17;
            if (t < 4)
                goto MatchNext;
            if (op + t > opEnd || ip + t > ipEnd)
                throw new CompressionException("LZO1X: output/input overrun in initial literal");
            for (int i = 0; i < t; i++)
                output[op++] = input[ip++];
            goto FirstLiteralRun;
        }

        // ---- Main loop ----
    MainLoop:
        if (ip >= ipEnd)
            throw new CompressionException("LZO1X: EOF marker not found");
        t = input[ip++];
        if (t >= 16)
            goto Match;

        // Literal run (t < 16).
        if (t == 0)
        {
            if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
            while (input[ip] == 0)
            {
                t += 255;
                ip++;
                if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
            }
            t += 15 + input[ip++];
        }
        if (op + t + 3 > opEnd || ip + t + 3 > ipEnd)
            throw new CompressionException("LZO1X: overrun copying literals");
        Copy4(output, op, input, ip);
        op += 4; ip += 4;
        if (--t > 0)
        {
            if (t >= 4)
            {
                do { Copy4(output, op, input, ip); op += 4; ip += 4; t -= 4; } while (t >= 4);
                if (t > 0)
                    for (int i = 0; i < t; i++) output[op++] = input[ip++];
            }
            else
            {
                for (int i = 0; i < t; i++) output[op++] = input[ip++];
            }
        }

    FirstLiteralRun:
        if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
        t = input[ip++];
        if (t >= 16)
            goto Match;

        // Short M2 match (2-3 bytes, distance via M2_MAX_OFFSET).
        {
            int mPos = op - 1 - M2_MAX_OFFSET - (t >> 2) - (input[ip++] << 2);
            if (mPos < outputStart || op + 3 > opEnd)
                throw new CompressionException("LZO1X: lookbehind/output overrun");
            output[op++] = output[mPos++];
            output[op++] = output[mPos++];
            output[op++] = output[mPos];
        }
        goto MatchDone;

    Match:
        {
            int matchOffset;
            if (t >= 64)
            {
                matchOffset = op - 1 - ((t >> 2) & 7) - (input[ip++] << 3);
                t = (t >> 5) - 1;
                if (matchOffset < outputStart || op + t + 2 > opEnd)
                    throw new CompressionException("LZO1X: overrun");
                goto CopyMatch;
            }
            else if (t >= 32)
            {
                t &= 31;
                if (t == 0)
                {
                    if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
                    while (input[ip] == 0)
                    {
                        t += 255;
                        ip++;
                        if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
                    }
                    t += 31 + input[ip++];
                }
                matchOffset = op - 1 - ((input[ip] | (input[ip + 1] << 8)) >> 2);
                ip += 2;
            }
            else if (t >= 16)
            {
                matchOffset = op - ((t & 8) << 11);
                t &= 7;
                if (t == 0)
                {
                    if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
                    while (input[ip] == 0)
                    {
                        t += 255;
                        ip++;
                        if (ip >= ipEnd) throw new CompressionException("LZO1X: input overrun");
                    }
                    t += 7 + input[ip++];
                }
                // Little-endian 16-bit offset field (minilzo get_unaligned_le16).
                matchOffset -= (input[ip] | (input[ip + 1] << 8)) >> 2;
                ip += 2;
                if (matchOffset == op)
                    goto EofFound;
                matchOffset -= 0x4000;
            }
            else
            {
                // Very short match (2 bytes).
                matchOffset = op - 1 - (t >> 2) - (input[ip++] << 2);
                if (matchOffset < outputStart || op + 2 > opEnd)
                    throw new CompressionException("LZO1X: overrun");
                output[op++] = output[matchOffset++];
                output[op++] = output[matchOffset];
                goto MatchDone;
            }

            if (matchOffset < outputStart || op + t + 2 > opEnd)
                throw new CompressionException("LZO1X: match overrun");

        CopyMatch:
            output[op++] = output[matchOffset++];
            output[op++] = output[matchOffset++];
            for (int i = 0; i < t; i++)
                output[op++] = output[matchOffset++];
        }

    MatchDone:
        t = input[ip - 2] & 3;
        if (t == 0)
            goto MainLoop;

    MatchNext:
        if (op + t > opEnd || ip + t > ipEnd)
            throw new CompressionException("LZO1X: literal overrun after match");
        if (t > 0) output[op++] = input[ip++];
        if (t > 1) output[op++] = input[ip++];
        if (t > 2) output[op++] = input[ip++];

        // Per minilzo, after the trailing literals the NEXT instruction byte is read and
        // handled as a match token (all t values dispatch to Match), not as a literal run.
        if (ip >= ipEnd)
            throw new CompressionException("LZO1X: unexpected end before next instruction");
        t = input[ip++];
        goto Match;

    EofFound:
        return op - outputStart;
    }

    private static void Copy4(byte[] dst, int dstOffset, byte[] src, int srcOffset)
    {
        dst[dstOffset] = src[srcOffset];
        dst[dstOffset + 1] = src[srcOffset + 1];
        dst[dstOffset + 2] = src[srcOffset + 2];
        dst[dstOffset + 3] = src[srcOffset + 3];
    }

    // ---- Checksums ----

    /// <summary>
    /// Computes Adler32 checksum (zlib variant, used by lzop default).
    /// <para>计算 Adler32 校验和（lzop 默认使用的 zlib 变体）。</para>
    /// </summary>
    private static uint Adler32(byte[] data, int offset, int length)
    {
        uint a = 1, b = 0;
        int end = offset + length;
        for (int i = offset; i < end; i++)
        {
            a = (a + data[i]) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    /// <summary>
    /// Computes CRC32 (IEEE, used when F_H_CRC32 is set). Delegates to the shared XZ Crc32.
    /// <para>计算 CRC32（IEEE，设置 F_H_CRC32 时使用），委托给共享的 XZ Crc32。</para>
    /// </summary>
    private static uint Crc32Checksum(byte[] data, int offset, int length)
        => Internal.Xz.Crc32.Compute(new ReadOnlySpan<byte>(data, offset, length));

    // ---- Big-endian helpers ----

    private static void WriteUInt16BE(Stream stream, int value)
    {
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)(value & 0xFF));
    }

    private static void WriteUInt32BE(Stream stream, uint value)
    {
        stream.WriteByte((byte)((value >> 24) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)(value & 0xFF));
    }

    private static int ReadUInt16BE(byte[] data, ref int offset)
    {
        int v = (data[offset] << 8) | data[offset + 1];
        offset += 2;
        return v;
    }

    private static uint ReadUInt32BE(byte[] data, ref int offset)
    {
        uint v = (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        offset += 4;
        return v;
    }
}
