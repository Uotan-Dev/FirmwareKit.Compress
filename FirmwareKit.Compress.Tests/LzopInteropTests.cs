using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 真实 lzop 互操作测试：嵌入由真实 lzop 工具（Oberhumer LZO 2.10）生成的
/// LZO1X 压缩样本，验证本库能字节一致地解压真实压缩块（非 stored 模式）。
/// <para>Real lzop interoperability tests: embeds LZO1X-compressed samples produced by
/// the real lzop tool (Oberhumer LZO 2.10) and verifies byte-identical decompression
/// of real compressed blocks (not stored mode).</para>
/// <para>样本由 tools/ 下的真实 lzop.exe 生成，来源与生成命令记录在测试内注释。</para>
/// </summary>
public class LzopInteropTests
{
    // 源数据：重复文本行 "The quick brown fox jumps over the lazy dog 0123456789\n" × 200，共 6000 字节。
    private static readonly byte[] Source6k = BuildSource6k();

    private static byte[] BuildSource6k()
    {
        var line = "The quick brown fox jumps over the lazy dog 0123456789\n"u8;
        var data = new byte[6000];
        for (int i = 0; i < data.Length; i++)
            data[i] = line[i % line.Length];
        return data;
    }

    /// <summary>
    /// 真实 lzop -1（LZO1X-1 最快）压缩的 172 字节样本。
    /// <para>172-byte sample compressed with real `lzop -1` (LZO1X-1, fastest).</para>
    /// </summary>
    [Fact]
    public void RealLzop_Level1_DecompressesByteIdentically()
    {
        byte[] sample = Convert.FromBase64String(
            "iUxaTwANChoKEEAgoAlAAgEOMAAxAACBpGp9if0AAAAACXNyYzZrLmJpboONCLcAABdwAAAAbVbGsuwAKlRoZSBxdWljayBicm93" +
            "biBmb3gganVtcHMgb3ZlciB0aGUgbGF6eSBkb2cgMDEyMzQ1Njc4OQpUaGUgcSAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABrYAA0w" +
            "MTIzNDU2Nzg5ClRoZSBxEQAAAAAAAA==");

        Assert.Equal(Source6k, CompressionService.Decompress(sample, CompressionFormat.Lzop));
    }

    /// <summary>
    /// 真实 lzop -9（LZO1X-999 最佳）压缩的 156 字节样本。
    /// <para>156-byte sample compressed with real `lzop -9` (LZO1X-999, best).</para>
    /// </summary>
    [Fact]
    public void RealLzop_Level9_DecompressesByteIdentically()
    {
        byte[] sample = Convert.FromBase64String(
            "iUxaTwANChoKEEAgoAlAAwkOMAAxAACBpGp9if0AAAAACXNyYzZrLmJpboSBCMAAABdwAAAAXVbGsuwxVGhlIHF1aWNrIGJyb3du" +
            "IGZveCBqdW1wcyBvdmVyIHRYAwACbGF6eSBkb2cgMDEyMzQ1Njc4OQogAAAAAAAAAObYACAAAAAAAAAA5tgAIAAAAAAAAAAf2AAR" +
            "AAAAAAAA");

        Assert.Equal(Source6k, CompressionService.Decompress(sample, CompressionFormat.Lzop));
    }

    /// <summary>
    /// 真实 lzop -F（禁用全部校验和）压缩的 168 字节样本；flags 无校验和位，块头只有 8 字节。
    /// <para>168-byte sample compressed with real `lzop -F` (checksums disabled);
    /// flags carry no checksum bits and block headers are 8 bytes.</para>
    /// </summary>
    [Fact]
    public void RealLzop_NoChecksum_DecompressesByteIdentically()
    {
        byte[] sample = Convert.FromBase64String(
            "iUxaTwANChoKEEAgoAlAAQUOMAAwAACBpGp9if0AAAAACXNyYzZrLmJpboPGCLkAABdwAAAAbQAqVGhlIHF1aWNrIGJyb3duIGZv" +
            "eCBqdW1wcyBvdmVyIHRoZSBsYXp5IGRvZyAwMTIzNDU2Nzg5ClRoZSBxIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGtgADTAxMjM0" +
            "NTY3ODkKVGhlIHERAAAAAAAA");

        Assert.Equal(Source6k, CompressionService.Decompress(sample, CompressionFormat.Lzop));
    }

    /// <summary>
    /// 本库压缩的 lzop 能被同一实例解压，且块头符合 12 字节布局
    /// （uncompressed_size + compressed_size + uncompressed checksum）。
    /// <para>Library-produced lzop round-trips and uses the 12-byte block header layout.</para>
    /// </summary>
    [Fact]
    public void LibraryProducedLzop_Uses12ByteBlockHeader()
    {
        var compressed = CompressionService.Compress(Source6k, CompressionFormat.Lzop);
        Assert.Equal(Source6k, CompressionService.Decompress(compressed, CompressionFormat.Lzop));

        // magic(9) + version(2) + lib_version(2) + version_needed(2) + method(1) + level(1)
        // + flags(4) + mode(4) + mtime_low(4) + mtime_high(4) + name_len(1) + header checksum(4) = 38
        Assert.Equal(0x89, compressed[0]);
        Assert.Equal(0x4C, compressed[1]);
        Assert.Equal(0x5A, compressed[2]);
        Assert.Equal(0x4F, compressed[3]);

        // First block: u(4) + c(4) + u_csum(4) then data → data offset 38+12 = 50.
        Assert.Equal((uint)Source6k.Length, ReadUInt32BE(compressed, 38));
        Assert.Equal((uint)Source6k.Length, ReadUInt32BE(compressed, 42));
        Assert.Equal(Source6k[0], compressed[50]);
    }

    private static uint ReadUInt32BE(byte[] data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }
}
