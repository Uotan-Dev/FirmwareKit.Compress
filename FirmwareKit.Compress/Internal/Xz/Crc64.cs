namespace FirmwareKit.Compress.Internal.Xz;

/// <summary>
/// CRC-64（ECMA-182，多项式 0xC96C5795D7870F42 反射形式），用于 XZ 校验。
/// <para>CRC-64 (ECMA-182, reflected polynomial 0xC96C5795D7870F42), used for the XZ check.</para>
/// </summary>
internal static class Crc64
{
    private static readonly ulong[] Table = BuildTable();

    private static ulong[] BuildTable()
    {
        const ulong poly = 0xC96C5795D7870F42UL;
        var table = new ulong[256];
        for (ulong i = 0; i < 256; i++)
        {
            ulong crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    /// <summary>计算 CRC-64（初始值 0xFFFFFFFFFFFFFFFF，最终异或 0xFFFFFFFFFFFFFFFF）。</summary>
    public static ulong Compute(ReadOnlySpan<byte> data)
    {
        ulong crc = 0xFFFFFFFFFFFFFFFFUL;
        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFFFFFFFFFUL;
    }
}
