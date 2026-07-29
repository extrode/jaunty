#!/usr/bin/env bash
#
# Builds SQLite.Interop for macOS arm64, which System.Data.SQLite does not ship.
#
# System.Data.SQLite.Core provides natives for win-x86, win-x64, linux-x64 and
# osx-x64 only, and the osx-x64 binary is plain x86_64 Mach-O - it cannot be
# loaded into an arm64 process. Without this, every test that touches
# System.Data.SQLite on Apple silicon fails with DllNotFoundException.
#
# The result is written to artifacts/SQLite.Interop.dll and picked up by
# SQLiteInteropOsxArm64.targets, which copies it into the output of the test
# projects that need it. Nothing here affects CI or any other platform.
#
#   usage: ./build.sh [version]      (default: the version below)
#
# Run it once per machine, and again after bumping System.Data.SQLite.Core.

set -euo pipefail

VERSION="${1:-1.0.119}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS="$HERE/artifacts"
WORK="$ARTIFACTS/work"
OUTPUT="$ARTIFACTS/SQLite.Interop.dll"

if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
  echo "build.sh: this is only needed on macOS arm64 (found $(uname -s)/$(uname -m))." >&2
  exit 1
fi

PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
MANAGED="$PACKAGES/stub.system.data.sqlite.core.netstandard/$VERSION/lib/netstandard2.0/System.Data.SQLite.dll"

if [[ ! -f "$MANAGED" ]]; then
  echo "build.sh: $MANAGED not found." >&2
  echo "          Restore the test projects first (dotnet restore), or pass the right version." >&2
  exit 1
fi

mkdir -p "$WORK"

# ---------------------------------------------------------------------------
# 1. The published source drop, for interop.c and the sqlite3 amalgamation.
# ---------------------------------------------------------------------------

SOURCE_ZIP="$WORK/sqlite-netFx-source-$VERSION.0.zip"
SOURCE_DIR="$WORK/source"

if [[ ! -d "$SOURCE_DIR" ]]; then
  if [[ ! -f "$SOURCE_ZIP" ]]; then
    echo "==> downloading System.Data.SQLite $VERSION source"
    curl -sSL --fail -o "$SOURCE_ZIP" \
      "https://system.data.sqlite.org/blobs/$VERSION.0/sqlite-netFx-source-$VERSION.0.zip"
  fi
  echo "==> extracting"
  mkdir -p "$SOURCE_DIR"
  unzip -q -o "$SOURCE_ZIP" -d "$SOURCE_DIR"
fi

GENERIC="$SOURCE_DIR/SQLite.Interop/src/generic"
UNSAFE="$SOURCE_DIR/System.Data.SQLite/UnsafeNativeMethods.cs"

for required in "$GENERIC/interop.c" "$UNSAFE"; do
  if [[ ! -f "$required" ]]; then
    echo "build.sh: expected $required in the source drop; layout may have changed." >&2
    exit 1
  fi
done

# ---------------------------------------------------------------------------
# 2. Compile. The defines match Setup/compile-interop-assembly-release.sh from
#    the source drop, which is what the shipped binaries are built with; only
#    the architecture differs (that script hardcodes -arch x86_64).
# ---------------------------------------------------------------------------

DEFINES=(
  -DSQLITE_THREADSAFE=1 -DSQLITE_USE_URI=1 -DSQLITE_ENABLE_COLUMN_METADATA=1
  -DSQLITE_ENABLE_STAT4=1 -DSQLITE_ENABLE_FTS3=1 -DSQLITE_ENABLE_LOAD_EXTENSION=1
  -DSQLITE_ENABLE_RTREE=1 -DSQLITE_SOUNDEX=1 -DSQLITE_ENABLE_MEMORY_MANAGEMENT=1
  -DSQLITE_ENABLE_API_ARMOR=1 -DSQLITE_ENABLE_DBSTAT_VTAB=1 -DSQLITE_ENABLE_STMTVTAB=1
  -DSQLITE_ENABLE_UPDATE_DELETE_LIMIT=1 -DINTEROP_TEST_EXTENSION=1
  -DINTEROP_EXTENSION_FUNCTIONS=1 -DINTEROP_VIRTUAL_TABLE=1 -DINTEROP_COMPRESS_EXTENSION=1
  -DINTEROP_ZIPFILE_EXTENSION=1 -DINTEROP_FTS5_EXTENSION=1 -DINTEROP_PERCENTILE_EXTENSION=1
  -DINTEROP_TOTYPE_EXTENSION=1 -DINTEROP_REGEXP_EXTENSION=1 -DINTEROP_JSON1_EXTENSION=1
  -DINTEROP_SHA1_EXTENSION=1 -DINTEROP_SHA3_EXTENSION=1 -DINTEROP_SESSION_EXTENSION=1
)

echo "==> compiling (arm64)"
clang -c -O2 -fPIC -arch arm64 "${DEFINES[@]}" -I"$GENERIC/../core" \
  -o "$WORK/interop.o" "$GENERIC/interop.c"
clang -c -O2 -fPIC -arch arm64 -o "$WORK/shim.o"   "$HERE/src/arm64-varargs-shim.c"
clang -c -O2 -fPIC -arch arm64 -o "$WORK/stubs.o"  "$HERE/src/codec-stubs.c"

OBJECTS=("$WORK/interop.o" "$WORK/shim.o" "$WORK/stubs.o")
LINK=(-shared -arch arm64 -lm -lpthread -lz)

# ---------------------------------------------------------------------------
# 3. First-pass link, to enumerate what is actually exported, then derive the
#    mangled-name aliases from the installed managed assembly.
# ---------------------------------------------------------------------------

echo "==> resolving mangled export names"
clang "${LINK[@]}" -o "$WORK/pass1.dylib" "${OBJECTS[@]}"
nm -gU "$WORK/pass1.dylib" > "$WORK/exports.txt"

# Built and invoked rather than `dotnet run`, so that build output cannot end up
# in aliases.txt - it is a linker response file, and stray text is a link error.
dotnet build "$HERE/MangleMap/MangleMap.csproj" -c Release -o "$WORK/manglemap" --nologo -v quiet >/dev/null
dotnet "$WORK/manglemap/manglemap.dll" "$MANAGED" "$UNSAFE" "$WORK/exports.txt" > "$WORK/aliases.txt"

if [[ ! -s "$WORK/aliases.txt" ]]; then
  echo "build.sh: no aliases were derived; refusing to produce a library that cannot bind." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# 4. Relink with the aliases applied.
# ---------------------------------------------------------------------------

echo "==> linking"
clang "${LINK[@]}" -o "$OUTPUT" "${OBJECTS[@]}" "@$WORK/aliases.txt"

# A sanity check worth having: the point of the whole exercise is that the managed
# assembly's imports resolve, so assert that every requested alias is really there.
sed 's/.*,_\([^,]*\)$/\1/' "$WORK/aliases.txt" | sort -u > "$WORK/requested.txt"
nm -gU "$OUTPUT" | awk '{ sub(/^_/, "", $NF); print $NF }' | sort -u > "$WORK/linked.txt"

if ! missing="$(comm -23 "$WORK/requested.txt" "$WORK/linked.txt")" || [[ -n "$missing" ]]; then
  echo "build.sh: the linked library is missing aliases that were requested:" >&2
  echo "$missing" | sed 's/^/  /' >&2
  exit 1
fi

echo
echo "$(file -b "$OUTPUT")"
echo "$OUTPUT ($(wc -c < "$OUTPUT" | tr -d ' ') bytes, $(nm -gU "$OUTPUT" | grep -c ' _SI') mangled exports)"
echo
echo "Rebuild the test projects to copy it into their output."
