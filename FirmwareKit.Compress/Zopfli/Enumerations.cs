namespace FirmwareKit.Compress.Internal.Zopfli
{
    /// <summary>
    /// Zopfli 输出的容器格式。
    /// <para>Container format produced by Zopfli.</para>
    /// </summary>
    public enum ZopfliFormat
    {
        /// <summary>
        /// GZIP 容器（RFC 1952）。
        /// <para>GZIP container (RFC 1952).</para>
        /// </summary>
        ZOPFLI_FORMAT_GZIP,

        /// <summary>
        /// ZLIB 容器（RFC 1950）。
        /// <para>ZLIB container (RFC 1950).</para>
        /// </summary>
        ZOPFLI_FORMAT_ZLIB,

        /// <summary>
        /// 原始 DEFLATE 流（RFC 1951，无容器头）。
        /// <para>Raw DEFLATE stream (RFC 1951, no container header).</para>
        /// </summary>
        ZOPFLI_FORMAT_DEFLATE
    }
}
