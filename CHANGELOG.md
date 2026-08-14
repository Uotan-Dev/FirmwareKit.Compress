# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 与
[Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 新增 / Added

- 编码端多核并行压缩：`CompressionOptions.MaxDegreeOfParallelism`（null/1=串行，默认）。
  - 自研 XZ (LZMA2) 编码器按固定 2 MiB 窗口并行，输出与串行逐字节一致；
  - Zopfli 按切分块并行（`ZopfliOptions.MaxDegreeOfParallelism`），输出与串行逐字节一致；
  - gzip/zlib/deflate/bzip2/zstd 按 1 MiB 块生成独立成员/帧后拼接为确定性多成员流；
  - brotli（.NET 解码器不支持串联流）与 lz4（K4os 解码器对 ≥512 KB 串联帧不可靠）暂不并行。
- 解码端支持多成员流（仍单线程）：bzip2 改用 `decompressConcatenated=true`；
  zlib/deflate 改用 SharpCompress 解码器并按 `TotalIn` 逐成员解压。

## [1.0.0] - 2026-08-13

### 新增 / Added

- 全托管压缩/解压类库（无 P/Invoke、无 unsafe、无原生依赖），目标框架 `netstandard2.0`、`net8.0`、`net10.0`。
- 支持格式：gzip、zopfli、zlib、deflate、brotli、xz、lzma、bzip2、lz4（标准/legacy/lg）、lzop、zstd。
- 自带纯 C# 实现：XZ (LZMA2) 编码器、Zopfli deflate 编码器、LZO1X 解压器；CRC-32 委托微软 `System.IO.Hashing`。
- 统一 API：`byte[]`、`Stream`、文件三个层级的压缩/解压，`CompressionFormat` 枚举统一调度。
- 魔数自动检测（gzip/zstd/bzip2/xz/lzop/lz4 家族）与 LZMA/zlib 启发式检测。
- 格式注册表（名称、别名、扩展名、魔数、能力标记）与名称/扩展名解析。
- 命令行前端 `FirmwareKit.Compress.Cli`（AOT 发布，`PublishAot=true`）。
- 生成 API 参考 XML 手册（`GenerateDocumentationFile`）。

### 修复 / Fixed

- LZOP：修正魔数为 9 字节（`89 4C 5A 4F 00 0D 0A 1A 0A`），块头布局与真实 `lzop` 工具一致（12 字节：大小×2 + 未压缩校验和），按 flags 位解析校验和存在性。
- LZO1X 解压器：对照 minilzo 参考修正 `MatchNext` 分支、16 位偏移字段字节序、首字节指令语义与块头长度。
- 与真实 `lzop` 工具双向互操作验证（-1/-9/-F 变体），可解压第三方库生成的 LZO1X 压缩块。

### 元数据 / Metadata

- 版本 `1.0.0`，由根目录 `Directory.Build.props` 统一管理。
- MIT 许可证，Copyright (c) 2026 Killy Jack。

[1.0.0]: https://github.com/Uotan-Dev/FirmwareKit.Compress/releases/tag/v1.0.0
