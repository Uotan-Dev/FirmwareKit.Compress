#!/usr/bin/env bash
#
# generate-real-files.sh — 为 FirmwareKit.Compress.Tests 生成各格式“真实”压缩文件。
# 优先使用系统外部工具（gzip/bzip2/xz/brotli/python zlib/lz4），
# 其余格式（zstd/lz4_legacy/lz4_lg/lzop/zopfli）使用本库 CLI 生成。
# 每种格式的产物与来源工具记录在 <dest>/manifest.txt。
#
# Usage: generate-real-files.sh [source-file] [dest-dir]
#   source-file  真实源文件（默认：FirmwareKit.MagiskBoot 测试仓库的 boot.img，若存在；
#                否则回退到本库 Release 构建的 FirmwareKit.Compress.dll）
#   dest-dir     输出目录（默认：FirmwareKit.Compress.Tests/RealFiles）
#
# 环境变量（可选）：BROTLI_QUALITY（默认 11）、ZSTD_LEVEL（默认 19）。

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_PROJ="$ROOT/FirmwareKit.Compress.Cli/FirmwareKit.Compress.Cli.csproj"

DEFAULT_SOURCE="$ROOT/../FirmwareKit.MagiskBoot/FirmwareKit.MagiskBoot.Tests/boot.img"
[ -f "$DEFAULT_SOURCE" ] || DEFAULT_SOURCE="$ROOT/FirmwareKit.Compress/bin/Release/net10.0/FirmwareKit.Compress.dll"

SOURCE="${1:-$DEFAULT_SOURCE}"
DEST="${2:-$ROOT/FirmwareKit.Compress.Tests/RealFiles}"
BROTLI_QUALITY="${BROTLI_QUALITY:-11}"
ZSTD_LEVEL="${ZSTD_LEVEL:-19}"

[ -f "$SOURCE" ] || { echo "error: source not found: $SOURCE" >&2; exit 1; }

echo "==> building CLI (Release)"
dotnet build "$CLI_PROJ" -c Release --nologo -v q >/dev/null

mkdir -p "$DEST"
cp "$SOURCE" "$DEST/sample.bin"
MANIFEST="$DEST/manifest.txt"
: > "$MANIFEST"

cli_compress() { # <format> <out> [level]
    local fmt="$1" out="$2" level="${3:-}"
    local args=(compress -f "$fmt")
    [ -n "$level" ] && args+=(-l "$level")
    dotnet run --project "$CLI_PROJ" -c Release --no-build -- "${args[@]}" "$DEST/sample.bin" "$out" >/dev/null
}

note() { echo "$1" >> "$MANIFEST"; }

if command -v gzip >/dev/null 2>&1; then
    gzip -k -c "$DEST/sample.bin" > "$DEST/sample.bin.gz"; note "gzip: gzip (external)"
else
    cli_compress gzip "$DEST/sample.bin.gz"; note "gzip: firmwarekit.cli"
fi

if command -v bzip2 >/dev/null 2>&1; then
    bzip2 -k -c "$DEST/sample.bin" > "$DEST/sample.bin.bz2"; note "bzip2: bzip2 (external)"
else
    cli_compress bzip2 "$DEST/sample.bin.bz2"; note "bzip2: firmwarekit.cli"
fi

if command -v xz >/dev/null 2>&1; then
    xz -k -c "$DEST/sample.bin" > "$DEST/sample.bin.xz"; note "xz: xz (external)"
else
    cli_compress xz "$DEST/sample.bin.xz"; note "xz: firmwarekit.cli"
fi

if command -v xz >/dev/null 2>&1; then
    xz -k -c --format=lzma "$DEST/sample.bin" > "$DEST/sample.bin.lzma"; note "lzma: xz --format=lzma (external)"
else
    cli_compress lzma "$DEST/sample.bin.lzma"; note "lzma: firmwarekit.cli"
fi

if command -v brotli >/dev/null 2>&1; then
    brotli -q "$BROTLI_QUALITY" -c "$DEST/sample.bin" > "$DEST/sample.bin.br"; note "brotli: brotli (external, q=$BROTLI_QUALITY)"
else
    cli_compress brotli "$DEST/sample.bin.br"; note "brotli: firmwarekit.cli"
fi

if python -c "import zlib" >/dev/null 2>&1; then
    python -c "import zlib,sys; d=open(sys.argv[1],'rb').read(); open(sys.argv[2],'wb').write(zlib.compress(d,9))" \
        "$DEST/sample.bin" "$DEST/sample.bin.zlib"
    note "zlib: python zlib (external)"
else
    cli_compress zlib "$DEST/sample.bin.zlib"; note "zlib: firmwarekit.cli"
fi

if python -c "import zlib" >/dev/null 2>&1; then
    python -c "import zlib,sys; c=zlib.compressobj(9,zlib.DEFLATED,-15); d=open(sys.argv[1],'rb').read(); open(sys.argv[2],'wb').write(c.compress(d)+c.flush())" \
        "$DEST/sample.bin" "$DEST/sample.bin.deflate"
    note "deflate: python zlib raw (external)"
else
    cli_compress deflate "$DEST/sample.bin.deflate"; note "deflate: firmwarekit.cli"
fi

if command -v lz4 >/dev/null 2>&1; then
    lz4 -c -9 -f "$DEST/sample.bin" > "$DEST/sample.bin.lz4"
    note "lz4: lz4 (external)"
elif python -c "import lz4.frame" >/dev/null 2>&1; then
    python -c "import lz4.frame,sys; d=open(sys.argv[1],'rb').read(); open(sys.argv[2],'wb').write(lz4.frame.compress(d,compression_level=9))" \
        "$DEST/sample.bin" "$DEST/sample.bin.lz4"
    note "lz4: python lz4.frame (external)"
else
    cli_compress lz4 "$DEST/sample.bin.lz4"; note "lz4: firmwarekit.cli"
fi

if command -v zstd >/dev/null 2>&1; then
    zstd -c -q -"$ZSTD_LEVEL" -f "$DEST/sample.bin" > "$DEST/sample.bin.zstd"; note "zstd: zstd (external, level $ZSTD_LEVEL)"
elif python -c "import zstandard" >/dev/null 2>&1; then
    python -c "import zstandard,sys; d=open(sys.argv[1],'rb').read(); c=zstandard.ZstdCompressor(level=int(sys.argv[3])); open(sys.argv[2],'wb').write(c.compress(d))" \
        "$DEST/sample.bin" "$DEST/sample.bin.zstd" "$ZSTD_LEVEL"
    note "zstd: python zstandard (external, level $ZSTD_LEVEL)"
else
    cli_compress zstd "$DEST/sample.bin.zstd" "$ZSTD_LEVEL"; note "zstd: firmwarekit.cli (level $ZSTD_LEVEL)"
fi
cli_compress lz4_legacy "$DEST/sample.bin.lz4_legacy"; note "lz4_legacy: firmwarekit.cli"
cli_compress lz4_lg "$DEST/sample.bin.lz4_lg"; note "lz4_lg: firmwarekit.cli"
cli_compress lzop "$DEST/sample.bin.lzop"; note "lzop: firmwarekit.cli (stored)"
cli_compress zopfli "$DEST/sample.bin.zopfli"; note "zopfli: firmwarekit.cli"

echo "==> generated $(ls "$DEST"/sample.bin.* 2>/dev/null | wc -l) files into $DEST"
echo "    source: $(basename "$SOURCE") ($(stat -c%s "$SOURCE") bytes), manifest: $MANIFEST"
