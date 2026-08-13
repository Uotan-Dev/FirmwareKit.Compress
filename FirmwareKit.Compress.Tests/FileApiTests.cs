using Xunit;

namespace FirmwareKit.Compress.Tests;

/// <summary>
/// 文件 API 测试：压缩/解压文件、自动检测解压、文件格式检测。
/// </summary>
public class FileApiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FirmwareKit.Compress.Tests." + Guid.NewGuid().ToString("N"));

    public FileApiTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* 临时目录清理失败可忽略 */ }
    }

    [Theory]
    [InlineData(CompressionFormat.Gzip)]
    [InlineData(CompressionFormat.Zlib)]
    [InlineData(CompressionFormat.Deflate)]
    [InlineData(CompressionFormat.Brotli)]
    [InlineData(CompressionFormat.Bzip2)]
    [InlineData(CompressionFormat.Xz)]
    [InlineData(CompressionFormat.Zstd)]
    [InlineData(CompressionFormat.Lz4)]
    [InlineData(CompressionFormat.Lz4Legacy)]
    [InlineData(CompressionFormat.Lz4Lg)]
    [InlineData(CompressionFormat.Lzma)]
    [InlineData(CompressionFormat.Zopfli)]
    [InlineData(CompressionFormat.Lzop)]
    public void CompressFile_DecompressFile_RoundTrip(CompressionFormat format)
    {
        string inputPath = Path.Combine(_dir, "input.bin");
        string compressedPath = inputPath + CompressionFormats.ToExtension(format);
        string outputPath = Path.Combine(_dir, "output.bin");

        var data = TestData.MakeText(16384);
        File.WriteAllBytes(inputPath, data);

        CompressionService.CompressFile(inputPath, compressedPath, format);
        CompressionService.DecompressFile(compressedPath, outputPath, format);

        Assert.Equal(data, File.ReadAllBytes(outputPath));
    }

    [Fact]
    public void DetectFile_IdentifiesFormat()
    {
        string gzPath = Path.Combine(_dir, "file.gz");
        File.WriteAllBytes(gzPath, CompressionService.Compress(TestData.MakeText(1024), CompressionFormat.Gzip));

        string xzPath = Path.Combine(_dir, "file.xz");
        File.WriteAllBytes(xzPath, CompressionService.Compress(TestData.MakeText(1024), CompressionFormat.Xz));

        Assert.Equal(CompressionFormat.Gzip, CompressionService.DetectFile(gzPath));
        Assert.Equal(CompressionFormat.Xz, CompressionService.DetectFile(xzPath));
    }

    [Fact]
    public void DetectFile_RawFile_ReturnsNone()
    {
        string rawPath = Path.Combine(_dir, "raw.bin");
        File.WriteAllBytes(rawPath, TestData.MakeRandom(128, 1));
        Assert.Equal(CompressionFormat.None, CompressionService.DetectFile(rawPath));
    }

    [Fact]
    public void DecompressFileAuto_DetectsAndDecompresses()
    {
        string inputPath = Path.Combine(_dir, "payload.bin");
        string compressedPath = Path.Combine(_dir, "payload.gz");
        string outputPath = Path.Combine(_dir, "payload.out");

        var data = TestData.MakeText(8192);
        File.WriteAllBytes(inputPath, data);
        CompressionService.CompressFile(inputPath, compressedPath, CompressionFormat.Gzip);

        CompressionService.DecompressFileAuto(compressedPath, outputPath);
        Assert.Equal(data, File.ReadAllBytes(outputPath));
    }

    [Fact]
    public void DecompressFileAuto_RawInput_PassesThrough()
    {
        string rawPath = Path.Combine(_dir, "raw.bin");
        string outputPath = Path.Combine(_dir, "raw.out");

        var data = TestData.MakeRandom(256, 2);
        File.WriteAllBytes(rawPath, data);

        CompressionService.DecompressFileAuto(rawPath, outputPath);
        Assert.Equal(data, File.ReadAllBytes(outputPath));
    }

    [Fact]
    public void CompressFile_MissingInput_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            CompressionService.CompressFile(Path.Combine(_dir, "missing.bin"), Path.Combine(_dir, "out.gz"), CompressionFormat.Gzip));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FileApi_NullOrEmptyPaths_Throw(string? path)
    {
        Assert.Throws<ArgumentNullException>(() => CompressionService.CompressFile(path!, "out", CompressionFormat.Gzip));
        Assert.Throws<ArgumentNullException>(() => CompressionService.DecompressFile(path!, "out", CompressionFormat.Gzip));
        Assert.Throws<ArgumentNullException>(() => CompressionService.DecompressFileAuto(path!, "out"));
        Assert.Throws<ArgumentNullException>(() => CompressionService.DetectFile(path!));
    }
}
