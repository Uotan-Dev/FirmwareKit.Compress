# FirmwareKit.Compress

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**FirmwareKit.Compress** 是 FirmwareKit 生态的压缩/解压类库：全托管实现，无 P/Invoke、无 unsafe、无任何原生依赖，开箱即用。提供统一的压缩/解压门面、格式枚举与注册表、魔数自动检测，以及一个功能完整的命令行前端。

**FirmwareKit.Compress** is the compression library of the FirmwareKit ecosystem: a fully-managed implementation with no P/Invoke, no unsafe and no native dependencies. It provides a unified compression/decompression facade, format enumeration and registry, magic-byte detection, and a fully-featured CLI frontend.

- 目标框架 / Target frameworks: `netstandard2.0`, `net8.0`, `net10.0`
- 包名 / Package: `FirmwareKit.Compress`
- 许可证 / License: MIT
- 仓库 / Repository: https://github.com/Uotan-Dev/FirmwareKit.Compress

## 特性 / Features

- **全托管**：gzip、zlib、deflate、brotli 基于 .NET 内置实现；bzip2、xz 解压基于 SharpCompress；lzma 基于 LZMA-SDK 移植；zstd 基于 ZstdSharp.Port；CRC-32 委托微软 `System.IO.Hashing`（硬件加速）；**XZ (LZMA2) 编码器、Zopfli deflate 编码器与 LZO1X 解压器为本库自带纯 C# 实现**。
  **Fully managed**: gzip/zlib/deflate/brotli on .NET built-ins; bzip2 and XZ decoding via SharpCompress; LZMA via the LZMA-SDK port; zstd via ZstdSharp.Port; CRC-32 delegated to Microsoft's `System.IO.Hashing` (hardware-accelerated); the **XZ (LZMA2) encoder, the Zopfli deflate encoder and the LZO1X decompressor are self-contained pure C# implementations**.
- **真实互操作**：XZ/LZMA 输出可被 `xz` 工具解压，LZOP 输出可被真实 `lzop` 工具读取，且可解压真实 `lzop`（含 -1/-9/-F 变体）与第三方库生成的 LZO1X 压缩块。
  **Real-world interop**: XZ/LZMA output is decodable by the `xz` tool, LZOP output is readable by the real `lzop` tool, and real LZO1X blocks produced by `lzop` (-1/-9/-F variants) or third-party libraries decompress byte-identically.
- **统一 API**：`byte[]`、`Stream`、文件三个层级的压缩/解压，格式由 `CompressionFormat` 枚举统一调度。
  **Unified API**: compression/decompression at the `byte[]`, `Stream` and file levels, dispatched by the `CompressionFormat` enum.
- **自动检测**：通过魔数识别 gzip、zstd、bzip2、xz、lzop、lz4 家族；LZMA / zlib 采用启发式检测。
  **Auto-detection**: magic-byte recognition for gzip, zstd, bzip2, xz, lzop and the LZ4 family; heuristic detection for LZMA / zlib.
- **MagiskBoot 兼容**：覆盖 magiskboot 的完整格式集合（gzip、zopfli、xz、lzma、bzip2、lz4、lz4_legacy、lz4_lg、lzop），可直接作为固件镜像解包/重打包的基础组件。
  **MagiskBoot compatible**: covers magiskboot's full format set (gzip, zopfli, xz, lzma, bzip2, lz4, lz4_legacy, lz4_lg, lzop), usable as the compression foundation for firmware image unpacking/repacking.

## 支持的格式 / Supported formats

| 格式 Format | 别名 Aliases | 扩展名 Extensions | 魔数 Magic | 压缩 Compress | 解压 Decompress | 流式 Streaming | 说明 Notes |
|---|---|---|---|---|---|---|---|
| `Gzip` | gz, gzip | .gz, .gzip | `1F 8B` | ✔ | ✔ | ✔ | RFC 1952 |
| `Zopfli` | zopfli | .gz | (gzip) | ✔ | ✔ | ✘ | 高压缩率 deflate，输出为标准 gzip |
| `Zlib` | zlib | .zlib, .zz | 启发式 | ✔ | ✔ | ✔ | RFC 1950 |
| `Deflate` | deflate, def | .deflate, .defl | — | ✔ | ✔ | ✔ | RFC 1951 原始流 |
| `Brotli` | brotli, br | .br, .brotli | — | ✔ | ✔ | ✔ | RFC 7932 |
| `Lz4` | lz4 | .lz4 | `04 22 4D 18` | ✔ | ✔ | ✔ | 标准帧 |
| `Lz4Legacy` | lz4_legacy | .lz4_legacy | `02 21 4C 18` | ✔ | ✔ | ✘ | magiskboot 块帧 |
| `Lz4Lg` | lz4_lg | .lz4_lg | `04 22 4D 40` | ✔ | ✔ | ✘ | LG 设备专用 |
| `Lzma` | lzma | .lzma | 启发式 | ✔ | ✔ | ✘ | .lzma 头（属性+字典+大小） |
| `Xz` | xz | .xz | `FD 37 7A 58 5A 00` | ✔ | ✔ | ✘ | LZMA2，自带纯 C# 编码器 |
| `Bzip2` | bzip2, bz2 | .bz2, .bzip2 | `42 5A 68` | ✔ | ✔ | ✔ | SharpCompress 托管实现 |
| `Lzop` | lzop, lzo | .lzop, .lzo | `89 4C 5A 4F 00 0D 0A 1A 0A` | ✔ | ✔ | ✘ | 存储模式 + 全托管 LZO1X 解压 |
| `Zstd` | zstd, zst | .zst, .zstd | `28 B5 2F FD` | ✔ | ✔ | ✘ | Zstandard |

## 快速开始 / Quick start

```bash
dotnet add package FirmwareKit.Compress
```

```csharp
using FirmwareKit.Compress;

// byte[] API：压缩 / 解压 / 自动检测
byte[] original = File.ReadAllBytes("payload.bin");
byte[] gz = CompressionService.Compress(original, CompressionFormat.Gzip);
byte[] back = CompressionService.Decompress(gz, CompressionFormat.Gzip);

var format = CompressionFormats.Detect(gz);            // Gzip
var parsed = CompressionFormats.Parse("xz");           // Xz
var ext = CompressionFormats.ToExtension(format);      // ".gz"

// Stream API：流式格式直接管道，块格式自动缓冲
using (var input = File.OpenRead("payload.bin"))
using (var output = File.Create("payload.xz"))
    CompressionService.Compress(input, output, CompressionFormat.Xz);

// 文件 API：自动检测并解压
CompressionService.DecompressFileAuto("payload.xz", "payload.bin");
```

### 压缩选项 / Compression options

```csharp
// 级别：gzip/zlib/deflate/brotli 0-9；zstd -5..22；lz4 0-12；null=默认
// (bzip2 接受该选项但由底层 SharpCompress 固定为默认级别)
var options = new CompressionOptions { Level = 9 };

// XZ/LZMA 字典大小（字节）
options.DictionarySize = 1u << 22;

// Zopfli 专用选项
options.Zopfli = new FirmwareKit.Compress.Compressors.ZopfliOptions
{
    NumIterations = 30,        // 迭代次数越多压缩率越高、越慢（默认 15）
    BlockSplitting = true,     // 启用块切分（默认 true）
};
```

### 格式注册表 / Format registry

`CompressionFormats` 提供格式元数据注册表：`All` 列出全部 13 种格式的
`CompressionFormatInfo`（名称、别名、扩展名、魔数、压缩/解压/流式支持），
并支持按扩展名（`FromExtension`）、按名称/别名（`Parse`/`TryParse`）与按魔数（`Detect`）解析格式。
`CompressionFormat.None` 视为恒等操作（原样透传），在检测失败与未压缩数据场景下使用。

## 命令行 / CLI

```bash
dotnet run --project FirmwareKit.Compress.Cli -- --help

# 压缩：显式格式 + 级别；默认按输入扩展名，否则 gzip
FirmwareKit.Compress.Cli compress -f xz -l 9 boot.img boot.img.xz
FirmwareKit.Compress.Cli compress boot.img                 # -> boot.img.gz
FirmwareKit.Compress.Cli compress=zopfli boot.img          # 旧语法亦可

# 解压：默认按魔数自动检测；-f 可强制指定格式
FirmwareKit.Compress.Cli decompress boot.img.xz            # -> boot.img
FirmwareKit.Compress.Cli decompress -f xz boot.img.xz boot.img

# 检测 / 枚举
FirmwareKit.Compress.Cli info boot.img.xz
FirmwareKit.Compress.Cli list
```

命令退出码：`0` 成功，`1` 出错（未知命令、文件不存在、格式不匹配等）。

## 构建与测试 / Build & test

```bash
dotnet build FirmwareKit.Compress.slnx -c Release
dotnet test  FirmwareKit.Compress.Tests -c Release
```

测试套件覆盖：格式注册表与扩展名/别名映射、魔数与启发式检测（含负例）、
全部 13 种格式的 `byte[]`/`Stream`/文件往返、与 .NET 内置 GZipStream / DeflateStream / ZLibStream 的互操作、
压缩比合理性、空/随机/大块数据、选项生效性、XZ CRC-64 校验路径、
真实 `lzop` 工具产物的 LZO1X 解压互操作，以及空参数/非法格式/损坏数据等错误路径。

## 生态 / Ecosystem

FirmwareKit 生态由若干面向固件处理场景的托管库组成：

- [FirmwareKit.MagiskBoot](https://github.com/Uotan-Dev/FirmwareKit.MagiskBoot) — Android boot 镜像解包/重打包/修补（本项目为其压缩基础）
- **FirmwareKit.Compress** — 本库：压缩/解压/检测/枚举

## 许可证 / License

MIT — 见 [LICENSE](LICENSE)。
