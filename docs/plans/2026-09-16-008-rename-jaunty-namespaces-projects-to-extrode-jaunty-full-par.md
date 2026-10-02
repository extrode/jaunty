# Rename `Jaunty.*` namespaces/projects to `Extrode.Jaunty.*` (full parity with JauntyQ)

## Context

`PackageId` for every published project already says `Extrode.Jaunty*` (e.g. `Extrode.Jaunty.FlatFiles`),
but the C# `namespace` keyword, the `src/`/`tests/`/`benchmarks/`/`tools/` folder names, the `.csproj`
filenames, and therefore the implicit `AssemblyName`/`RootNamespace` all still say bare `Jaunty.*`.
Found 2026-09-16 comparing against sibling project JauntyQ, which is fully consistent
(`Extrode.JauntyQ.*` everywhere: namespace, assembly, folder, and package id all agree).

This is more than cosmetic: `v1.0.0-rc.2` is already tagged and released on GitHub Packages
(2026-09-06), so anyone who installed it already writes `using Jaunty;` against a `Jaunty.dll`.
Owner decided (via AskUserQuestion) to do the full-parity rename anyway and accept it as a breaking
change, touching `src/` + `tests/` + `tools/` + `benchmarks/` + docs/README, matching JauntyQ's
convention exactly. This must ship with a CHANGELOG entry calling out the break.

## Scope confirmed by inventory

**Projects to rename (folder + `.csproj` filename; `git mv` both, so `AssemblyName`/`RootNamespace`
follow implicitly and now match the existing `PackageId`):**

- `src/`: `Jaunty`, `Jaunty.Extensions.Logging`, `Jaunty.Extensions.Npgsql`,
  `Jaunty.Extensions.Reflection`, `Jaunty.FlatFiles`, `Jaunty.FlatFiles.DuckDB`, `Jaunty.Fluent`,
  `Jaunty.Scaffolding`, `Jaunty.Scaffolding.Cli`, `Jaunty.SourceGenerator` (10)
- `tests/`: `Jaunty.FlatFiles.DuckDB.Tests`, `Jaunty.FlatFiles.Tests`, `Jaunty.Fluent.ConfigTests`,
  `Jaunty.Fluent.SourceGen.Tests`, `Jaunty.Fluent.Tests`, `Jaunty.Scaffolding.Cli.Tests`,
  `Jaunty.Scaffolding.Tests`, `Jaunty.SourceGenerator.Tests`, `Jaunty.Tests`, `Jaunty.UnitTests` (10)
- `benchmarks/`: `Jaunty.Benchmarks`, `Jaunty.FlatFiles.Benchmarks` (2)
- `tools/`: `Jaunty.Fuzz` (1)
- Each → prefix `Extrode.` (e.g. `Jaunty.Fluent` → `Extrode.Jaunty.Fluent`), folder and
  `<Name>.csproj` renamed together.
- **Not renamed**: `samples/NativeAOT-*`, `samples/torture-test-*` (their own project names don't
  carry `Jaunty`), `Jaunty.slnx` itself (product name, same pattern as `JauntyQ.slnx`).

**Content updates (no file rename, text only), via one word-bounded, negative-lookbehind regex
`(?<!Extrode\.)\bJaunty\b` → `Extrode.Jaunty` — safe because it does NOT match `JauntyQ`, `JauntyConfig`,
`JAUNTY_*` env vars, or already-correct `Extrode.Jaunty` occurrences (case-sensitive, word-bounded):**

- All 353 `namespace Jaunty...` declarations under `src/`, `tests/`, `tools/`, `benchmarks/`
- All `using Jaunty...` statements repo-wide (875 files), including the 5 `GlobalUsings.cs` files
- All 27 `InternalsVisibleTo` `_Parameter1` values in `src/*/*.csproj` (e.g. `Jaunty.Tests` →
  `Extrode.Jaunty.Tests`)
- All `ProjectReference Include="...Jaunty..."` paths, repo-wide (`src/`, `tests/`, `benchmarks/`,
  `samples/*` that reference Jaunty projects, `tools/`)
- `Jaunty.slnx` — update every `<Project Path="...">` entry to the new paths; leave `<Folder Name=...>`
  and the solution filename alone
- The two explicit `<RootNamespace>` overrides (`tests/Jaunty.UnitTests` → `Extrode.Jaunty.Tests`,
  `tests/Jaunty.Fluent.ConfigTests` → `Extrode.Jaunty.Fluent.Tests`)
- `.github/workflows/{ci,nightly,release}.yml` — `dotnet test --project tests/Jaunty...` paths,
  `dotnet restore/build Jaunty.slnx` (unaffected, solution file name unchanged), publish-path
  references like `src/Jaunty.Scaffolding.Cli/...`
- `scripts/*.ps1`, `scripts/*.py` referencing project/assembly paths
- `.editorconfig`, `coverage.runsettings`, `Directory.Build.props`, `src/Directory.Build.props`,
  `Directory.Packages.props`, `docs/01-api-reference/docfx.json`,
  `tests/*/stryker-config.json`, `tests/*/appsettings*.json` — check each for path/namespace
  references (not all hits will need changes; some are unrelated `Jaunty` mentions like repo URLs)
- `tests/Jaunty.UnitTests/Unit/SolutionLayoutTests.cs` — asserts the solution file's layout; will
  need its expected strings updated (covered by the same regex, but verify by reading it first)

**Docs/README — narrower, manual-reviewed pass, NOT the blanket regex:**
Only rewrite C# code fences (`using Jaunty...`, `namespace Jaunty...`) and explicit path/file
references (`src/Jaunty/Jaunty.csproj`, `Jaunty.dll`). Leave prose product-name mentions
("Jaunty is a micro-ORM...") as `Jaunty` — the product name is not changing, only the C# namespace.

**Explicitly out of scope:** GitHub repo name/URL (`github.com/extrode/jaunty`, lowercase, unrelated),
`PackageId` values (already correct), `PUBLISH_TO_NUGET_ORG`/`JAUNTY_*` env vars, `CHANGELOG.md`
historical entries (append a new entry, don't rewrite history), the product name itself anywhere.

## Script

Write `scripts/rename-namespace-extrode-prefix.sh` (bash, since this is a one-shot repo-wide
mechanical operation, not a recurring cleanup script — doesn't need the `scripts/cleanup/`
dry-run/`--execute` interface):

1. `git mv` each of the 23 project folders + their `.csproj` file to the `Extrode.`-prefixed name
   (list above), in one pass, failing loudly if any expected path is missing.
2. Run the regex substitution `(?<!Extrode\.)\bJaunty\b` → `Extrode.Jaunty` over:
   `**/*.cs` (excluding `bin/`, `obj/`), `**/*.csproj`, `Jaunty.slnx`, `.github/workflows/*.yml`,
   `scripts/*.ps1`, `scripts/*.py`, `.editorconfig`, `coverage.runsettings`, `Directory.Build.props`,
   `src/Directory.Build.props`, `Directory.Packages.props`, `docs/01-api-reference/docfx.json`,
   `tests/*/stryker-config.json`, `tests/*/appsettings*.json`
   (use `perl -pi -e` for lookbehind support; PowerShell's `-replace` also supports `(?<!...)`
   if run cross-platform is preferred — pick one, don't mix).
3. Leave docs/README to a manual pass (Edit tool, reviewed diff) — script does not touch `*.md`.
4. Print a summary of files changed for review before commit.

## Verification

1. `git status` — review the full diff before building.
2. `dotnet restore Jaunty.slnx && dotnet build Jaunty.slnx -c Release --no-incremental` — must be
   clean across all TFMs (netstandard2.0, net472, net8.0, net10.0).
3. Run the full test suite project-by-project as CI does (`tests/Extrode.Jaunty.Tests`,
   `.UnitTests`, `.Fluent.Tests`, `.Fluent.ConfigTests`, `.Fluent.SourceGen.Tests`,
   `.Scaffolding.Tests`, `.Scaffolding.Cli.Tests`, `.SourceGenerator.Tests`, `.FlatFiles.Tests`,
   `.FlatFiles.DuckDB.Tests`) — must match current pass/skip counts exactly.
4. `git grep -n '\bJaunty\.' -- '*.cs' '*.csproj'` (excluding `bin/`/`obj/`/`samples/torture-test-*`
   own namespaces) should return nothing left un-prefixed.
5. Manually re-read README.md and spot-check 2-3 docs pages to confirm code fences updated and
   prose untouched.
6. Add a `CHANGELOG.md` `[Unreleased]` entry noting the breaking namespace/assembly rename.

## Branch / commit

Standard tier: branch off `dev` (`refactor/extrode-jaunty-namespace-parity`), single script-driven
commit for the mechanical rename + a second commit for the manual docs pass, merge `--no-ff` into
`dev` once build+tests are green. Not pushed (owner's call per standing convention).
