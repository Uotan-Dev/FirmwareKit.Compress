namespace FirmwareKit.Compress.Internal.Zopfli;

/// <summary>
/// Zopfli 算法运行配置（替代原 CLI 程序中的 Globals 静态类）。
/// <para>Zopfli algorithm run configuration (replaces the Globals static class from the original CLI).</para>
/// <para>
/// 使用 [ThreadStatic] 保证并发调用互不干扰：每个线程独立持有自己的配置副本，
/// 未显式设置时回退到与原版 CLI 一致的默认值（numiterations=15、blocksplitting=1、
/// blocksplittingmax=15、输出格式 gzip）。
/// </para>
/// <para>
/// Fields are [ThreadStatic] so concurrent calls on different threads never interfere;
/// when unset, they fall back to the original CLI defaults (numiterations=15,
/// blocksplitting=1, blocksplittingmax=15, gzip output).
/// </para>
/// </summary>
internal static class Globals
{
    [ThreadStatic] private static int? _numiterations;
    [ThreadStatic] private static int? _blocksplitting;
    [ThreadStatic] private static int? _blocksplittingmax;
    [ThreadStatic] private static ZopfliFormat? _outputType;
    [ThreadStatic] private static int? _verbose;
    [ThreadStatic] private static int? _verboseMore;
    [ThreadStatic] private static int? _maxdop;

    /// <summary>是否输出基本诊断信息（默认 0=关闭）。<para>Whether to print basic diagnostics (default 0=off).</para></summary>
    public static int verbose
    {
        get => _verbose ?? 0;
        set => _verbose = value;
    }

    /// <summary>是否输出详细诊断信息（默认 0=关闭）。<para>Whether to print detailed diagnostics (default 0=off).</para></summary>
    public static int verbose_more
    {
        get => _verboseMore ?? 0;
        set => _verboseMore = value;
    }

    /// <summary>LZ77 优化迭代次数（默认 15）。<para>LZ77 optimization iterations (default 15).</para></summary>
    public static int numiterations
    {
        get => _numiterations ?? 15;
        set => _numiterations = value;
    }

    /// <summary>是否启用块切分（默认 1=启用）。<para>Whether block splitting is enabled (default 1=enabled).</para></summary>
    public static int blocksplitting
    {
        get => _blocksplitting ?? 1;
        set => _blocksplitting = value;
    }

    /// <summary>最大切分块数（默认 15，0 表示不限）。<para>Maximum number of split blocks (default 15, 0 means unlimited).</para></summary>
    public static int blocksplittingmax
    {
        get => _blocksplittingmax ?? 15;
        set => _blocksplittingmax = value;
    }

    /// <summary>输出格式（默认 gzip）。<para>Output format (default gzip).</para></summary>
    public static ZopfliFormat output_type
    {
        get => _outputType ?? ZopfliFormat.ZOPFLI_FORMAT_GZIP;
        set => _outputType = value;
    }

    /// <summary>多核并行度（null/1=串行，>1=按切分块并行）。<para>Multi-core parallelism (null/1 = sequential, &gt;1 = parallel per split block).</para></summary>
    public static int? maxdop
    {
        get => _maxdop;
        set => _maxdop = value;
    }
}
