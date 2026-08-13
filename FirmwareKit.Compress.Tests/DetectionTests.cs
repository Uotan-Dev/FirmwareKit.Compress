using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 魔数检测测试：固定魔数、启发式（LZMA/ZLIB）、流检测与负例。
/// </summary>
public class DetectionTests
{
    [Theory]
    [InlineData(new byte[] { 0x1F, 0x8B }, CompressionFormat.Gzip)]
    [InlineData(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, CompressionFormat.Zstd)]
    [InlineData(new byte[] { 0x42, 0x5A, 0x68 }, CompressionFormat.Bzip2)]
    [InlineData(new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, CompressionFormat.Xz)]
    [InlineData(new byte[] { 0x89, 0x4C, 0x5A, 0x4F, 0x00, 0x0D, 0x0A, 0x1A, 0x0A }, CompressionFormat.Lzop)]
    [InlineData(new byte[] { 0x04, 0x22, 0x4D, 0x18 }, CompressionFormat.Lz4)]
    [InlineData(new byte[] { 0x04, 0x22, 0x4D, 0x40 }, CompressionFormat.Lz4Lg)]
    [InlineData(new byte[] { 0x02, 0x21, 0x4C, 0x18 }, CompressionFormat.Lz4Legacy)]
    public void Detect_FixedMagics_AreRecognized(byte[] magic, CompressionFormat expected)
    {
        Assert.Equal(expected, CompressionFormats.Detect(magic));
    }

    [Fact]
    public void Detect_LzmaHeader_IsRecognized()
    {
        Assert.Equal(CompressionFormat.Lzma, CompressionFormats.Detect(CreateLzmaHeader(0x5D, 0x1000, -1)));
        Assert.Equal(CompressionFormat.Lzma, CompressionFormats.Detect(CreateLzmaHeader(0x00, 0x1000, -1)));
        Assert.Equal(CompressionFormat.Lzma, CompressionFormats.Detect(CreateLzmaHeader(0xE0, 0x8000000, -1)));
        Assert.Equal(CompressionFormat.Lzma, CompressionFormats.Detect(CreateLzmaHeader(0x5D, 1 << 27, -1)));
    }

    [Fact]
    public void Detect_InvalidLzmaHeaders_ReturnNone()
    {
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(CreateLzmaHeader(0xFF, 0x1000, -1)));
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(CreateLzmaHeader(0xE1, 0x1000, -1)));
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(CreateLzmaHeader(0x5D, 0x800, -1)));     // dict too small
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(CreateLzmaHeader(0x5D, 0x1001, -1)));    // not power of 2
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(CreateLzmaHeader(0x5D, 0, -1)));         // dict zero
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(new byte[] { 0x5D, 0x00 }));            // too short
    }

    [Fact]
    public void Detect_ZlibHeader_IsRecognized()
    {
        Assert.Equal(CompressionFormat.Zlib, CompressionFormats.Detect(new byte[] { 0x78, 0x9C }));
        Assert.Equal(CompressionFormat.Zlib, CompressionFormats.Detect(new byte[] { 0x78, 0x01 }));
    }

    [Fact]
    public void Detect_InvalidZlibHeaders_ReturnNone()
    {
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(new byte[] { 0x08, 0x00 })); // (CMF<<8|FLG) % 31 != 0
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(new byte[] { 0x01, 0x01 })); // CM != 8
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(new byte[] { 0x78, 0x9D })); // 0x789D % 31 != 0
    }

    [Fact]
    public void Detect_EmptyAndShortData_ReturnNone()
    {
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(Array.Empty<byte>()));
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(new byte[] { 0x1F }));
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(new byte[] { 0x00, 0x01, 0x02, 0x03 }));
    }

    [Fact]
    public void Detect_NullArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CompressionFormats.Detect((byte[])null!));
    }

    [Fact]
    public void Detect_Stream_DetectsAndRestoresPosition()
    {
        var data = new byte[] { 0x1F, 0x8B, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        using var stream = new MemoryStream(data);

        Assert.Equal(CompressionFormat.Gzip, CompressionFormats.Detect(stream));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Detect_Stream_ShortInput_ReturnsNone()
    {
        using var stream = new MemoryStream(new byte[] { 0x1F });
        Assert.Equal(CompressionFormat.None, CompressionFormats.Detect(stream));
    }

    [Fact]
    public void Detect_Stream_NonSeekable_StillDetects()
    {
        using var inner = new MemoryStream(new byte[] { 0x42, 0x5A, 0x68, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        using var stream = new NonSeekableStream(inner);
        Assert.Equal(CompressionFormat.Bzip2, CompressionFormats.Detect(stream));
    }

    [Fact]
    public void Detect_Stream_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CompressionFormats.Detect((Stream)null!));
    }

    private static byte[] CreateLzmaHeader(byte propertyByte, int dictionarySize, long uncompressedSize)
    {
        var header = new byte[13];
        header[0] = propertyByte;
        header[1] = (byte)(dictionarySize & 0xFF);
        header[2] = (byte)((dictionarySize >> 8) & 0xFF);
        header[3] = (byte)((dictionarySize >> 16) & 0xFF);
        header[4] = (byte)((dictionarySize >> 24) & 0xFF);
        BitConverter.GetBytes(uncompressedSize).CopyTo(header, 5);
        return header;
    }
}

/// <summary>不可定位的测试流（模拟网络/管道输入）。</summary>
internal sealed class NonSeekableStream : Stream
{
    private readonly Stream _inner;

    public NonSeekableStream(Stream inner) => _inner = inner;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
