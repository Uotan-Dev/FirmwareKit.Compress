namespace FirmwareKit.Compress.Cli;

/// <summary>
/// FirmwareKit.Compress 命令行工具：压缩、解压、格式检测与格式枚举。
/// <para>FirmwareKit.Compress command-line tool: compress, decompress, format detection and format enumeration.</para>
/// <para>用法 / Usage: FirmwareKit.Compress.Cli &lt;command&gt; [args...]</para>
/// </summary>
public static class CompressCli
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }

            string command = args[0];

            if (command is "help" or "-h" or "--help")
            {
                PrintUsage();
                return 0;
            }

            if (command is "--version" or "-v")
            {
                Console.WriteLine($"FirmwareKit.Compress.Cli {Version}");
                return 0;
            }

            if (command == "list")
                return ListCommand();

            if (command == "info")
                return InfoCommand(args);

            if (command == "decompress")
                return DecompressCommand(args);

            if (command == "compress" || command.StartsWith("compress=", StringComparison.Ordinal))
                return CompressCommand(args);

            Console.Error.WriteLine($"Unknown command: {command}");
            PrintUsage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string Version =>
        typeof(CompressCli).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private static void PrintUsage()
    {
        Console.WriteLine("""
            FirmwareKit.Compress.Cli - 压缩/解压/检测/枚举工具
            A fully-managed compression CLI (gzip, zopfli, zlib, deflate, brotli, xz, lzma,
            bzip2, lz4, lz4_legacy, lz4_lg, lzop, zstd).

            Usage:
              FirmwareKit.Compress.Cli compress [-f <format>] [-l <level>] <infile> [outfile]
                  Compress a file. Format defaults to the input extension, then gzip.
                  压缩文件。格式默认取输入扩展名，否则为 gzip。
              FirmwareKit.Compress.Cli decompress [-f <format>] <infile> [outfile]
                  Decompress a file. Format defaults to automatic magic-byte detection.
                  解压文件。格式默认按魔数自动检测。
              FirmwareKit.Compress.Cli info <infile>
                  Detect and print the compression format of a file.
                  检测并打印文件的压缩格式。
              FirmwareKit.Compress.Cli list
                  List all supported compression formats.
                  列出所有受支持的压缩格式。
              FirmwareKit.Compress.Cli help | -h | --help
                  Show this help.
              FirmwareKit.Compress.Cli --version | -v
                  Show version.

            Options:
              -f, --format <fmt>  Compression format:
                                  gzip zopfli zlib deflate brotli lz4 lz4_legacy lz4_lg
                                  lzma xz bzip2 lzop zstd
              -l, --level <n>     Compression level (compress only; per-format range).
              Legacy: compress=fmt <infile> [outfile] is also accepted.
            """);
    }

    private static int ListCommand()
    {
        Console.WriteLine($"{"Id",-4} {"Name",-12} {"Extensions",-26} {"Magic",-22} C D S");
        Console.WriteLine(new string('-', 78));

        foreach (var info in CompressionFormats.All)
        {
            string magic = info.HasMagic ? Hex(info.Magic!) : "-";
            string extensions = string.Join(",", info.Extensions);
            Console.WriteLine(
                $"{(int)info.Format,-4} {info.Name,-12} {extensions,-26} {magic,-22} " +
                $"{(info.CanCompress ? "y" : "-")} {(info.CanDecompress ? "y" : "-")} " +
                $"{(info.IsStreaming ? "y" : "-")}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {CompressionFormats.All.Count} formats. C=compress D=decompress S=streaming");
        return 0;
    }

    private static int InfoCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("info: missing <infile>");
            return 1;
        }

        string inputPath = args[1];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"info: file not found: {inputPath}");
            return 1;
        }

        var format = CompressionService.DetectFile(inputPath);
        var info = CompressionFormats.GetInfo(format);
        var fileInfo = new FileInfo(inputPath);

        Console.WriteLine($"File:    {Path.GetFullPath(inputPath)}");
        Console.WriteLine($"Size:    {fileInfo.Length} bytes");

        if (format == CompressionFormat.None)
        {
            Console.WriteLine("Format:  unknown (not a recognized compressed format)");
            return 0;
        }

        Console.WriteLine($"Format:  {info!.Name} ({(int)format})");
        Console.WriteLine($"Magic:   {Hex(info.Magic!)}");
        Console.WriteLine($"Exts:    {string.Join(", ", info.Extensions)}");
        Console.WriteLine($"Aliases: {string.Join(", ", info.Aliases)}");
        Console.WriteLine($"Compress:   {(info.CanCompress ? "yes" : "no")}");
        Console.WriteLine($"Decompress: {(info.CanDecompress ? "yes" : "no")}");
        Console.WriteLine($"Streaming:  {(info.IsStreaming ? "yes" : "no")}");
        return 0;
    }

    private static int CompressCommand(string[] args)
    {
        CompressionFormat? explicitFormat = null;
        int? level = null;
        var positional = new List<string>();

        // 兼容旧语法 compress=format <infile>。
        if (args[0].StartsWith("compress=", StringComparison.Ordinal))
        {
            string legacyFormat = args[0]["compress=".Length..];
            if (!CompressionFormats.TryParse(legacyFormat, out var parsed))
            {
                Console.Error.WriteLine($"compress: unknown format: {legacyFormat}");
                return 1;
            }
            explicitFormat = parsed;
        }

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if ((arg == "-l" || arg == "--level") && i + 1 < args.Length)
            {
                if (!int.TryParse(args[++i], out var parsedLevel))
                {
                    Console.Error.WriteLine("compress: invalid level value");
                    return 1;
                }
                level = parsedLevel;
            }
            else if ((arg == "-f" || arg == "--format") && i + 1 < args.Length)
            {
                if (!CompressionFormats.TryParse(args[++i], out var parsed))
                {
                    Console.Error.WriteLine($"compress: unknown format: {args[i]}");
                    return 1;
                }
                explicitFormat = parsed;
            }
            else if (arg.StartsWith("-", StringComparison.Ordinal) && arg != "-")
            {
                Console.Error.WriteLine($"compress: unknown option: {arg}");
                return 1;
            }
            else
            {
                positional.Add(arg);
            }
        }

        if (positional.Count < 1)
        {
            Console.Error.WriteLine("compress: missing <infile>");
            return 1;
        }

        string inputPath = positional[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"compress: file not found: {inputPath}");
            return 1;
        }

        var format = explicitFormat ?? CompressionFormats.FromExtension(inputPath);
        if (format == CompressionFormat.None)
            format = CompressionFormat.Gzip;

        string outputPath = positional.Count >= 2
            ? positional[1]
            : inputPath + CompressionFormats.ToExtension(format);

        var options = level.HasValue ? new CompressionOptions { Level = level } : null;
        CompressionService.CompressFile(inputPath, outputPath, format, options);

        var inputSize = new FileInfo(inputPath).Length;
        var outputSize = new FileInfo(outputPath).Length;
        Console.WriteLine($"Compressed {inputPath} -> {outputPath} " +
                          $"[{CompressionFormats.GetDisplayName(format)}] {inputSize} -> {outputSize} bytes");
        return 0;
    }

    private static int DecompressCommand(string[] args)
    {
        CompressionFormat? explicitFormat = null;
        var positional = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if ((arg == "-f" || arg == "--format") && i + 1 < args.Length)
            {
                if (!CompressionFormats.TryParse(args[++i], out var parsed))
                {
                    Console.Error.WriteLine($"decompress: unknown format: {args[i]}");
                    return 1;
                }
                explicitFormat = parsed;
            }
            else if (arg.StartsWith("-", StringComparison.Ordinal) && arg != "-")
            {
                Console.Error.WriteLine($"decompress: unknown option: {arg}");
                return 1;
            }
            else
            {
                positional.Add(arg);
            }
        }

        if (positional.Count < 1)
        {
            Console.Error.WriteLine("decompress: missing <infile>");
            return 1;
        }

        string inputPath = positional[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"decompress: file not found: {inputPath}");
            return 1;
        }

        string? outputPath = positional.Count >= 2
            ? positional[1]
            : DeriveOutputPath(inputPath);

        if (outputPath == null)
        {
            Console.Error.WriteLine("decompress: cannot derive output name, specify <outfile>");
            return 1;
        }

        if (explicitFormat.HasValue)
        {
            CompressionService.DecompressFile(inputPath, outputPath, explicitFormat.Value);
        }
        else
        {
            CompressionService.DecompressFileAuto(inputPath, outputPath);
        }

        Console.WriteLine($"Decompressed {inputPath} -> {outputPath}");
        return 0;
    }

    /// <summary>
    /// 去掉输入文件名中匹配的压缩扩展名来推导输出名（file.gz -> file）。
    /// <para>Derives the output name by stripping the matching compression extension (file.gz -> file).</para>
    /// </summary>
    private static string? DeriveOutputPath(string inputPath)
    {
        string fileName = Path.GetFileName(inputPath);
        string? directory = Path.GetDirectoryName(inputPath);

        foreach (var info in CompressionFormats.All)
        {
            foreach (var ext in info.Extensions)
            {
                if (!fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    continue;

                string stripped = fileName[..^ext.Length];
                if (stripped.Length == 0)
                    continue;

                return string.IsNullOrEmpty(directory)
                    ? stripped
                    : Path.Combine(directory, stripped);
            }
        }

        return null;
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
