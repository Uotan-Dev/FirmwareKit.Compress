using System.IO.Compression;
using System.Runtime.InteropServices;

namespace FirmwareKit.Compress.Internal;

/// <summary>
/// 统一的 netstandard2.0 垫片层：集中处理各目标框架间缺失的 BCL API 差异，
/// 使业务代码统一使用新版语法（<c>Array.Fill</c>、<c>Stream.Write(span)</c>、
/// <c>CollectionsMarshal.AsSpan</c>、<c>BitOperations.TrailingZeroCount</c> 等），
/// 并在现代框架上保留零分配/硬件加速的最优路径。
/// <para>Unified netstandard2.0 polyfill layer: centralizes BCL API differences between
/// target frameworks so calling code uses modern syntax uniformly while the fast,
/// zero-allocation / hardware-accelerated paths are kept on modern frameworks.</para>
/// </summary>
internal static class Polyfill
{
    // -------------------------------------------------------------------------------
    // Array.Fill
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Fills an array with a value (polyfill for <c>Array.Fill</c>).
    /// <para>用指定值填充整个数组（Array.Fill 的垫片）。</para>
    /// </summary>
    public static void Fill<T>(T[] array, T value)
    {
#if NETSTANDARD2_0
        for (int i = 0; i < array.Length; i++)
            array[i] = value;
#else
        Array.Fill(array, value);
#endif
    }

    /// <summary>
    /// Fills a range of an array with a value (polyfill for <c>Array.Fill&lt;T&gt;</c>).
    /// <para>用指定值填充数组指定范围（Array.Fill 的垫片）。</para>
    /// </summary>
    public static void Fill<T>(T[] array, T value, int startIndex, int count)
    {
#if NETSTANDARD2_0
        int end = startIndex + count;
        for (int i = startIndex; i < end; i++)
            array[i] = value;
#else
        Array.Fill(array, value, startIndex, count);
#endif
    }

    // -------------------------------------------------------------------------------
    // Stream.Write(ReadOnlySpan<byte>)
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Writes a span to a stream (polyfill for <c>Stream.Write(ReadOnlySpan&lt;byte&gt;)</c>).
    /// On netstandard2.0 this incurs a single pooled copy; modern frameworks write the span directly.
    /// <para>将 span 写入流（Stream.Write(ReadOnlySpan) 的垫片）。netstandard2.0 下会产生一次
    /// 临时拷贝，现代框架直接写入 span。</para>
    /// </summary>
    public static void WriteTo(Stream stream, ReadOnlySpan<byte> buffer)
    {
#if NETSTANDARD2_0
        byte[] tmp = buffer.ToArray();
        stream.Write(tmp, 0, tmp.Length);
#else
        stream.Write(buffer);
#endif
    }

    // -------------------------------------------------------------------------------
    // CollectionsMarshal.AsSpan(List<byte>)
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Returns a span over a list's backing buffer (polyfill for
    /// <c>CollectionsMarshal.AsSpan</c>). On netstandard2.0 a copy is returned.
    /// <para>获取 List 底层缓冲的 span（CollectionsMarshal.AsSpan 的垫片）；
    /// netstandard2.0 下返回副本。</para>
    /// </summary>
    public static ReadOnlySpan<byte> AsSpan(List<byte> list)
    {
#if NET5_0_OR_GREATER
        return CollectionsMarshal.AsSpan(list);
#else
        return list.ToArray();
#endif
    }

    // -------------------------------------------------------------------------------
    // MemoryMarshal.GetArrayDataReference
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Returns a reference to the first element of an array (polyfill for
    /// <c>MemoryMarshal.GetArrayDataReference</c>).
    /// <para>获取数组首元素的引用（MemoryMarshal.GetArrayDataReference 的垫片）。</para>
    /// </summary>
    public static ref T GetArrayDataReference<T>(T[] array)
    {
#if NET5_0_OR_GREATER
        return ref MemoryMarshal.GetArrayDataReference(array);
#else
        return ref MemoryMarshal.GetReference(array.AsSpan());
#endif
    }

    // -------------------------------------------------------------------------------
    // BitOperations.TrailingZeroCount
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Counts trailing zero bits (polyfill for <c>BitOperations.TrailingZeroCount</c>).
    /// <para>统计尾部连续零位（BitOperations.TrailingZeroCount 的垫片）。</para>
    /// </summary>
    public static int TrailingZeroCount(ulong value)
    {
#if NET6_0_OR_GREATER
        return System.Numerics.BitOperations.TrailingZeroCount(value);
#else
        if (value == 0)
            return 64;
        int count = 0;
        while ((value & 1) == 0)
        {
            value >>= 1;
            count++;
        }
        return count;
#endif
    }

    // -------------------------------------------------------------------------------
    // CompressionLevel — SmallestSize does not exist on netstandard2.0
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Maps a unified integer level (0-9) to <see cref="CompressionLevel"/>. Uses
    /// <c>SmallestSize</c> when available (net6+), falling back to <c>Optimal</c> otherwise.
    /// <para>将统一整数级别（0-9）映射为 CompressionLevel；net6+ 使用 SmallestSize，
    /// 旧框架回退到 Optimal。</para>
    /// </summary>
    public static CompressionLevel MapCompressionLevel(int? level, CompressionLevel @default = CompressionLevel.Optimal)
    {
        if (!level.HasValue)
            return @default;

#if NET6_0_OR_GREATER
        return level.Value switch
        {
            <= 0 => CompressionLevel.NoCompression,
            <= 3 => CompressionLevel.Fastest,
            >= 9 => CompressionLevel.SmallestSize,
            _ => CompressionLevel.Optimal
        };
#else
        return level.Value switch
        {
            <= 0 => CompressionLevel.NoCompression,
            <= 3 => CompressionLevel.Fastest,
            _ => CompressionLevel.Optimal
        };
#endif
    }

    // -------------------------------------------------------------------------------
    // BrotliStream factory — the concrete type differs by TFM
    // (System.IO.Compression.BrotliStream on net8+/net10; BrotliSharpLib.BrotliStream on netstandard2.0).
    // Both derive from Stream, so the factory returns Stream and call sites stay identical.
    // -------------------------------------------------------------------------------

    /// <summary>
    /// Creates a Brotli compression stream for the target framework.
    /// <para>为目标框架创建 Brotli 压缩流。</para>
    /// </summary>
    public static Stream CreateBrotliCompressor(Stream output, int? level, bool leaveOpen)
    {
#if NETSTANDARD2_0
        var brotli = new BrotliSharpLib.BrotliStream(output, CompressionMode.Compress, leaveOpen);
        brotli.SetQuality(CompressionLevelMapper.ToBrotliQuality(level));
        return brotli;
#else
        return new BrotliStream(output, Polyfill.MapCompressionLevel(level), leaveOpen);
#endif
    }

    /// <summary>
    /// Creates a Brotli decompression stream for the target framework.
    /// <para>为目标框架创建 Brotli 解压流。</para>
    /// </summary>
    public static Stream CreateBrotliDecompressor(Stream input, bool leaveOpen)
    {
#if NETSTANDARD2_0
        return new BrotliSharpLib.BrotliStream(input, CompressionMode.Decompress, leaveOpen);
#else
        return new BrotliStream(input, CompressionMode.Decompress, leaveOpen);
#endif
    }
}
