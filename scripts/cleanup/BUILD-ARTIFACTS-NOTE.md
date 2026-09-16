# Build artifacts cleanup

Written 2026-09-16 during a machine-wide disk-space audit. This project was carrying
**5.50 GB** across 55 build/tool-output directories (bin, obj, node_modules, dist,
and similar -- see `build-artifacts-cleanup-2026-09-16.ps1` in this same folder for the exact
list and sizes at the time this was written).

## What this is

`build-artifacts-cleanup-2026-09-16.ps1` deletes exactly the directories listed inside it. It:

- **Does nothing by default.** Bare invocation is a dry run.
- Deletes only with `-e` / `--execute`.
- Re-checks every path against `git ls-files` at run time before deleting it, and refuses
  (leaves alone) anything git tracks -- even if it's on the list below, even if this note is
  stale. A stale list makes the script a no-op on those entries, not a data-loss risk.
- Reports "already gone" instead of erroring on anything already cleaned up by hand.

Run it from PowerShell, from anywhere (it resolves its own repo root):

```
pwsh -NoProfile scripts/cleanup/build-artifacts-cleanup-2026-09-16.ps1        # dry run
pwsh -NoProfile scripts/cleanup/build-artifacts-cleanup-2026-09-16.ps1 -e     # delete
```

## Keep this updated

**If the build/artifact layout changes -- new output directories, moved paths, renamed or removed
projects inside this repo -- update the list inside the `.ps1`, not just this note.** The script
is the source of truth for what gets deleted; this file just explains why it exists. A stale
script isn't dangerous (the git-tracked check protects committed content either way), but it will
silently stop reclaiming space as new artifact directories appear that aren't on its list.

## What was on the list as of 2026-09-16

- `tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/bin` (894 MB)
- `benchmarks/Extrode.Jaunty.FlatFiles.Benchmarks/bin` (835 MB)
- `tests/Extrode.Jaunty.UnitTests/bin` (753 MB)
- `tests/Extrode.Jaunty.Tests/bin` (440 MB)
- `tests/Extrode.Jaunty.Scaffolding.Tests/bin` (316 MB)
- `tests/Extrode.Jaunty.SourceGenerator.Tests/bin` (286 MB)
- `tests/Extrode.Jaunty.Scaffolding.Cli.Tests/bin` (284 MB)
- `tests/Extrode.Jaunty.Fluent.SourceGen.Tests/bin` (284 MB)
- `benchmarks/Extrode.Jaunty.Benchmarks/bin` (264 MB)
- `tests/Extrode.Jaunty.Fluent.Tests/bin` (175 MB)
- `samples/NativeAOT-FluentQuery/bin` (141 MB)
- `tests/Extrode.Jaunty.Fluent.ConfigTests/bin` (139 MB)
- `samples/NativeAOT-WithReflection/bin` (139 MB)
- `samples/NativeAOT-CustomMapper/bin` (139 MB)
- `samples/NativeAOT-Basic/bin` (139 MB)
- `tests/Extrode.Jaunty.FlatFiles.Tests/bin` (125 MB)
- `src/Extrode.Jaunty.Scaffolding.Cli/bin` (63 MB)
- `tests/Extrode.Jaunty.Tests/obj` (19 MB)
- `src/Extrode.Jaunty/obj` (19 MB)
- `src/Extrode.Jaunty.Extensions.Reflection/bin` (18 MB)
- `src/Extrode.Jaunty/bin` (16 MB)
- `tests/Extrode.Jaunty.UnitTests/obj` (14 MB)
- `src/Extrode.Jaunty.FlatFiles/bin` (14 MB)
- `src/Extrode.Jaunty.Extensions.Npgsql/bin` (14 MB)
- `src/Extrode.Jaunty.Extensions.Logging/bin` (14 MB)
- `src/Extrode.Jaunty.Fluent/bin` (13 MB)
- `src/Extrode.Jaunty.FlatFiles.DuckDB/bin` (11 MB)
- `dist` (8 MB)
- `tests/Extrode.Jaunty.Fluent.Tests/obj` (6 MB)
- `tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/obj` (6 MB)
- `src/Extrode.Jaunty.Fluent/obj` (5 MB)
- `src/Extrode.Jaunty.Scaffolding/bin` (5 MB)
- `tools/Extrode.Jaunty.Fuzz/bin` (4 MB)
- `tests/Extrode.Jaunty.SourceGenerator.Tests/obj` (3 MB)
- `tests/Extrode.Jaunty.Scaffolding.Tests/obj` (3 MB)
- `tests/Extrode.Jaunty.FlatFiles.Tests/obj` (2 MB)
- `src/Extrode.Jaunty.Extensions.Reflection/obj` (2 MB)
- `src/Extrode.Jaunty.FlatFiles.DuckDB/obj` (2 MB)
- `tests/Extrode.Jaunty.Scaffolding.Cli.Tests/obj` (2 MB)
- `tests/Extrode.Jaunty.Fluent.SourceGen.Tests/obj` (2 MB)
- `benchmarks/Extrode.Jaunty.Benchmarks/obj` (2 MB)
- `tests/Extrode.Jaunty.Fluent.ConfigTests/obj` (1 MB)
- `src/Extrode.Jaunty.FlatFiles/obj` (1 MB)
- `benchmarks/Extrode.Jaunty.FlatFiles.Benchmarks/obj` (1 MB)
- `src/Extrode.Jaunty.Scaffolding.Cli/obj` (1 MB)
- `src/Extrode.Jaunty.Extensions.Logging/obj` (1 MB)
- `src/Extrode.Jaunty.SourceGenerator/obj` (1 MB)
- `src/Extrode.Jaunty.Extensions.Npgsql/obj` (1 MB)
- `samples/NativeAOT-FluentQuery/obj` (1 MB)
- `src/Extrode.Jaunty.Scaffolding/obj` (1 MB)
- `samples/NativeAOT-WithReflection/obj` (1 MB)
- `samples/NativeAOT-CustomMapper/obj` (1 MB)
- `samples/NativeAOT-Basic/obj` (1 MB)
- `src/Extrode.Jaunty.SourceGenerator/bin` (1 MB)
- `tools/Extrode.Jaunty.Fuzz/obj` (0 MB)
