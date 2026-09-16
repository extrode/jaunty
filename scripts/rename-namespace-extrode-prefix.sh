#!/usr/bin/env bash
# One-shot mechanical rename: Jaunty.* -> Extrode.Jaunty.* namespaces/projects,
# for parity with JauntyQ's Extrode.JauntyQ.* convention. See
# docs/plans/ (branch refactor/extrode-jaunty-namespace-parity) for the full rationale.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

PROJECTS=(
  "src/Jaunty"
  "src/Jaunty.Extensions.Logging"
  "src/Jaunty.Extensions.Npgsql"
  "src/Jaunty.Extensions.Reflection"
  "src/Jaunty.FlatFiles"
  "src/Jaunty.FlatFiles.DuckDB"
  "src/Jaunty.Fluent"
  "src/Jaunty.Scaffolding"
  "src/Jaunty.Scaffolding.Cli"
  "src/Jaunty.SourceGenerator"
  "tests/Jaunty.FlatFiles.DuckDB.Tests"
  "tests/Jaunty.FlatFiles.Tests"
  "tests/Jaunty.Fluent.ConfigTests"
  "tests/Jaunty.Fluent.SourceGen.Tests"
  "tests/Jaunty.Fluent.Tests"
  "tests/Jaunty.Scaffolding.Cli.Tests"
  "tests/Jaunty.Scaffolding.Tests"
  "tests/Jaunty.SourceGenerator.Tests"
  "tests/Jaunty.Tests"
  "tests/Jaunty.UnitTests"
  "benchmarks/Jaunty.Benchmarks"
  "benchmarks/Jaunty.FlatFiles.Benchmarks"
  "tools/Jaunty.Fuzz"
)

echo "== renaming ${#PROJECTS[@]} project folders + csproj files =="
for old_path in "${PROJECTS[@]}"; do
  dir=$(dirname "$old_path")
  old_name=$(basename "$old_path")
  new_name="Extrode.$old_name"
  new_path="$dir/$new_name"

  if [[ ! -d "$old_path" ]]; then
    echo "ERROR: expected folder missing: $old_path" >&2
    exit 1
  fi
  if [[ ! -f "$old_path/$old_name.csproj" ]]; then
    echo "ERROR: expected csproj missing: $old_path/$old_name.csproj" >&2
    exit 1
  fi

  git mv "$old_path/$old_name.csproj" "$old_path/$new_name.csproj"
  git mv "$old_path" "$new_path"
  echo "  $old_path -> $new_path"
done

echo "== rewriting Jaunty -> Extrode.Jaunty in code/config files =="
# (?<!Extrode\.)\bJaunty\b : word-bounded, skips already-prefixed Extrode.Jaunty,
# does not match JauntyQ, JauntyConfig, JAUNTY_* env vars (case-sensitive).
FILES=$(
  {
    find src tests tools benchmarks -type f -name "*.cs" ! -path "*/bin/*" ! -path "*/obj/*"
    find src tests tools benchmarks samples -type f -name "*.csproj" ! -path "*/bin/*" ! -path "*/obj/*"
    echo "Jaunty.slnx"
    find .github/workflows -type f -name "*.yml"
    find scripts -type f \( -name "*.ps1" -o -name "*.py" \)
    echo ".editorconfig"
    echo "coverage.runsettings"
    echo "Directory.Build.props"
    echo "src/Directory.Build.props"
    echo "Directory.Packages.props"
    echo "docs/01-api-reference/docfx.json"
    find tests -type f -name "stryker-config.json"
    find tests -type f -name "appsettings*.json"
  } | sort -u
)

count=0
for f in $FILES; do
  [[ -f "$f" ]] || continue
  if grep -qP '(?<!Extrode\.)\bJaunty\b' "$f"; then
    perl -CSD -pi -e 's/(?<!Extrode\.)\bJaunty\b/Extrode.Jaunty/g' "$f"
    count=$((count + 1))
    echo "  updated: $f"
  fi
done
echo "== $count files updated =="
