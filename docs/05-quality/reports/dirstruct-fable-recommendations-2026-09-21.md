# Directory-structure convention: second opinion (2026-09-21)

Requested from Fable as an independent review of `dirstruct-comparison-2026-09-21.md`, given the
same two trees plus a broader survey of the other .NET repos under `C:/home/code/extrode.com/`
(see that survey inline in section 2's framing — not reproduced here). Fable verified everything
about Jaunty against the working tree directly; JauntyQ was read via the semble index and the
committed tree file only (no direct file access in that session), so treat JauntyQ-specific claims
below as slightly less independently verified than the Jaunty-specific ones, though nothing here
contradicts what's observable from the tree.

This corrects several claims in the original comparison report — do not treat that report as final
on its own; read this alongside it.

## 1. Where the comparison report is wrong or incomplete

1. **The "two fixture philosophies" split does not exist.** JauntyQ's `samples/*/db/{schema,tables/<T>,ddl,migrations}` is the product's *consumer* convention: it is what the Roslyn generator reads (`db/schema/jaunty.schema.json`, `db/ddl/*.sql`, `db/tables/<T>/*.sql`, migrations simulated on top; see `docs/06-reference/configuration.md` "Schema-source precedence" and `docs/03-guides/ddl-as-schema-source.md`). Those folders are sample source code, not test fixtures, and the five Conduit copies are five consumer projects each targeting one dialect. Centralising them would break the thing the samples exist to demonstrate. Jaunty's `data/` is genuinely test infrastructure (ci.yml, nightly.yml, `reset-test-databases.{ps1,sh}` and `NorthwindDatabase.cs` all consume it). Different concerns, no conflict, no decision needed.
2. **Jaunty's `seed/` is dead, not a philosophy.** `seed/{mariadb,mysql,postgres,mssql}/README.md` say "To be added in torture-test steps"; the `schema.sql` files are "reference copy only" for the eShopOnWeb torture port (runtime DDL is embedded C#); `seed/sakila/README.md` documents gitignored external clones. Nothing under scripts, CI, csproj or tests references `seed/`. The report describes `data/` as "4 schema JSON fixtures"; it actually holds the per-dialect `create-northwind.sql` + `create-stored-procedures.sql`, `sqlite/Northwind.db` and `basic.csv`.
3. **`Extrode.Jaunty.UnitTests` cannot be folded into `Extrode.Jaunty.Tests`.** The csproj header explains the split: `Extrode.Jaunty.Tests` runs serially (shared live DBs, process-global `JauntyConfig` mutation, one `xunit.runner.json` per assembly); `UnitTests` holds only tests that touch none of that and runs parallel. Same reason `Extrode.Jaunty.Fluent.ConfigTests` exists. The split stays; only the names change.
4. **The `UnitTests/Unit/` nesting is load-bearing.** `RootNamespace` is `Extrode.Jaunty.Tests` so moved files keep their `Extrode.Jaunty.Tests.Unit.*` namespaces and can move between the two halves without edits. Not redundant; keep it.
5. **Missed: `docs/07-design` is not design docs.** It holds the docs-site visual design (`*.dc.html`, `support.js`). It does not belong in the numbered content sequence.
6. **Missed: `docs/benchmark-artifacts/` commits raw BenchmarkDotNet logs** (nine `*.log` files from 2026-09-02) into docs. Logs are artifacts; the `results/*.md|csv` reports are the only keepable part.
7. **Missed: stale `tests/{database,mariadb,mysql,postgres}-setup.sql`.** `tests/Helpers/TEST-SETUP.md` already disowns them in favour of `data/`.
8. **Missed: sample naming diverges more than tests do.** Jaunty samples are kebab-case (`NativeAOT-Basic`, `torture-test-*`) with nested `src/`+`tests/`; JauntyQ samples are `Extrode.JauntyQ.<Corpus>.<Dialect>.Tests`. Also only 4 of Jaunty's 8 sample projects are in `Jaunty.slnx`.
9. **Missed: `tests/Helpers/` in Jaunty contains one file, a markdown doc.** The real shared helpers live in `Extrode.Jaunty.Tests/Helpers` and are `<Compile Include>`-linked into `UnitTests`, which is the same mechanism as JauntyQ's `test-infra/FixtureGate.cs` linked via `tests/Directory.Build.props` and `samples/Directory.Build.props`.
10. `.config/dotnet-tools.json` in Jaunty pins `dotnet-stryker 4.16.0`. JauntyQ's nightly installs Stryker in a workflow step instead. That is an oversight in JauntyQ, not an optional difference.

## 2. Top-level convention (all extrode.com .NET repos)

| Dir | Meaning | Rule |
|---|---|---|
| `src/` | product source, single-deliverable repo | `src/Extrode.<Product>[.<Feature>]/` |
| `apps/` + `libs/` | product source, multi-deliverable repo | use instead of `src/`, never alongside it |
| `tests/` | test projects + `tests/Shared/` linked sources | see section 3 |
| `samples/` | consumer-style projects | see section 4 |
| `benchmarks/` | `Extrode.<Product>[.<Feature>].Benchmarks` | already aligned |
| `tools/` | dev-time tooling projects and native build helpers | already aligned |
| `scripts/` | build/test/release scripts, `scripts/cleanup/` for reviewed cleanup scripts | already aligned |
| `docs/` | see section 5 | |
| `db/` | the application's own schema source of truth (DDL, migrations) | product repos only; JauntyQ consumer projects use the same name inside each project |
| `data/` | committed seed/fixture inputs shared by more than one project | per-project inputs colocate in the project instead |
| `dist/` | release deliverables that are deliberately committed (Jaunty `dist/docs-site`) | otherwise ignored and fed from `artifacts/` |
| `artifacts/` | all generated output: test results, coverage, Stryker, BenchmarkDotNet, publish, packages | gitignored; replaces root `TestResults/`, `BenchmarkDotNet.Artifacts/`, `StrykerOutput/`, `publish/` |
| `tmp/`, `work/`, `.worktrees/` | scratch, private notes (junction allowed), agent worktrees | gitignored |
| `.config/dotnet-tools.json` | local tool manifest | required wherever a dotnet tool is run in CI |
| `.github/`, `.claude/` | as today | |

Pick between `src/` and `apps/`+`libs/` by deliverable count, not by taste: one NuGet family or one app means `src/`; a repo shipping several runnables plus shared libraries means `apps/`+`libs/`. Both are "source roots" and everything else in the table is identical, so the broader repos need only rename their generated-output and archive folders, not restructure. Specific outliers to fold in later: `strata/lib` to `src`; `epass/99-archives` and `ajar/_archive` to `docs/99-archive`; `nativeweb/docs-site`, `flat/site` to `dist/docs-site` if committed or `artifacts/docs-site` if not; root `TestResults`, `publish`, `BenchmarkDotNet.Artifacts`, `StrykerOutput` to `artifacts/`.

Optional: the .NET 8+ SDK `UseArtifactsOutput` property moves `bin/obj/publish/package` under `artifacts/` too. Evaluate before adopting; Jaunty's tests locate `data/*.db` relative to output paths and net472 already needs `AppendRuntimeIdentifierToOutputPath=false`.

## 3. `tests/`

- Name: `Extrode.<Product>[.<Feature>].Tests`. `.Tests` is always the last segment. When one feature needs two assemblies for runner-configuration reasons, the qualifier goes before `.Tests` and names the real axis:
  - `Extrode.Jaunty.UnitTests` renames to `Extrode.Jaunty.Parallel.Tests` (the split is serial vs parallel, not unit vs integration; `Extrode.Jaunty.Tests` has a large `Unit/` tree of its own). If you prefer a non-runner word, `Isolated.Tests`; do not use `Unit.Tests`.
  - `Extrode.Jaunty.Fluent.ConfigTests` renames to `Extrode.Jaunty.Fluent.Config.Tests`.
  - Rename cost: `Jaunty.slnx`, `ci.yml` (lines 241, 280, 361), `nightly.yml` (146, 285, 297-298), `release.yml` comment, its `stryker-config.json`, `SolutionLayoutTests.cs`.
- Keep the bare `Extrode.Jaunty.Tests`. It is the core suite; that it also covers `Extensions.Reflection` is a coverage-map fact, not a naming defect. Splitting it would add a fourth serial assembly for no runtime gain.
- Internal layout: first level is by test kind only where the project actually has more than one kind (`Unit/`, `Integration/`, `Performance/`), then mirrors the `src/` concern folders. Jaunty's `Fluent.Tests` mixing `Unit/`, `Integration/` with bare `Entities/`, `Helpers/`, `data/` is fine: those three are support folders, not test folders, and match the JauntyQ pattern. Normalise the names to `Entities/`, `Helpers/`, `data/` everywhere.
- Shared linked sources: `tests/Shared/` in both repos, no csproj, linked via `tests/Directory.Build.props` (and `samples/Directory.Build.props` where samples are tests). JauntyQ moves `test-infra/` there. Jaunty moves `TEST-SETUP.md` to `docs/06-development/test-setup.md` and deletes `tests/Helpers/`.

## 4. `samples/` and fixture data (the ruling)

- JauntyQ's per-sample `db/` stays exactly as is. It is the product convention.
- Jaunty keeps root `data/` as its shared seed home. Do not move it under `tests/`: CI, both reset scripts, the docker bulk-insert overlay and `NorthwindDatabase.cs` all path to it, and the gain is cosmetic.
- Delete Jaunty `seed/`. Move the four `schema.sql` reference copies into `samples/torture-test-eshoponweb-port/db/` (that repo-local `db/` is the same semantics as JauntyQ's), and fold `seed/sakila/README.md` into `samples/torture-test-sakila-codegen/README.md`, which already links to it.
- Delete Jaunty `tests/*-setup.sql` (four files) and JauntyQ's empty `data/`.
- Sample project naming: the directory equals the csproj name, PascalCase, no hyphens; multi-project samples get their own `src/` + `tests/` (Jaunty's torture ports already do). Do not require the `Extrode.` prefix in samples; they model consumer code, and JauntyQ's Conduit ports already use consumer namespaces. Jaunty's `NativeAOT-Basic` and friends become `NativeAotBasic` etc. when convenient; low priority. All sample projects join the `.slnx` under `/samples/`.

## 5. `docs/` numbering

Numbers must mean the same thing in every repo, and folders that do not apply are omitted, not renumbered (agree with the report). The map should follow reader order (Diátaxis: tutorial, how-to, reference, explanation) then contributor material. Recommended map:

| Prefix | Name | Jaunty today | JauntyQ today |
|---|---|---|---|
| 00 | `00-overview` | `00-quick-start` (README, why) | same |
| 01 | `01-getting-started` | `00-quick-start` (build-and-test) | same |
| 02 | `02-learn` | `08-learn` | same |
| 03 | `03-guides` | new; `04-extensions/flatfiles` how-tos | same |
| 04 | `04-reference` | `01-api-reference`, `04-extensions` reference | `06-reference` |
| 05 | `05-architecture` | `02-architecture` | new when needed |
| 06 | `06-development` | `03-development`, `00-quick-start/project-layout` | new when needed |
| 07 | `07-quality` | `05-quality` | new when needed |
| 08 | `08-releases` | `06-releases` | new when needed |
| 09 | `09-roadmap` | new when needed | `07-roadmap` |
| 99 | `99-archive` | same | new when needed |

Unnumbered peers, same in every repo: `_assets/`, `decisions/`, `plans/`, `specs/`, `lessons/`, `laws/`, `handoffs/`. Jaunty's `07-design` moves to `_assets/site-design/`; `benchmark-artifacts/*.log` are deleted and `benchmark-artifacts/results/*` move to `07-quality/reports/benchmarks/`; `architecture/.gitkeep` is deleted (already done, see `dirstruct-comparison-2026-09-21.md`); JauntyQ's `assets/build-not-prod.svg` moves to `_assets/`.

Cost is honest: Jaunty renames 8 numbered folders and every relative link; JauntyQ renames 2. `scripts/check-doc-links.mjs` exists to catch breakage and `dist/docs-site` must be regenerated in the same commit. The zero-churn fallback is to declare Jaunty's current numbering canonical and renumber only JauntyQ, but JauntyQ's `02-learn`/`03-guides` have no Jaunty slot below 08, so the fallback produces a worse reading order permanently to save one afternoon now. Fable would not take that fallback.

## 6. Adopt from each other

- `laws/` + `Unit/Laws/L00N*Tests.cs`: worth adopting in JauntyQ once it has a stated invariant to guard. Not now.
- `handoffs/`: keep in JauntyQ (used 2026-09-19 for a cross-repo probe). Jaunty covers the same need with the memory system plus `plans/`; add `handoffs/` there only when a handoff is actually written.
- `.config/dotnet-tools.json`: add to JauntyQ now, pinning the Stryker version the nightly currently installs ad hoc.

## 7. What would change this recommendation

- If the shared Docs tool (`C:\home\code\beparey.com\docsgen`, unrenamed per [[docs-generator-extraction-plan]]) hardcodes prefix-to-section names, the map in section 5 must follow the tool, not the other way round. Fable could not read that path in its sandboxed session — verify before acting on section 5.
- If the public flip of Jaunty (per [[jaunty-rc2-release-state]]) is imminent, do the section 5 rename after the flip, not before; everything else in this doc is safe pre-flip.
- If any live consumer of `seed/` turns up outside the repo (the private `jaunty-audit` junction, per [[jaunty-private-work-junction]]), keep `seed/sakila/README.md` where it is until that consumer is repointed.

Other things noticed, not acted on: `docs/conventions.md` says non-audit cleanup scripts live flat in `scripts/`, but 25+ non-audit scripts sit in `scripts/cleanup/`; `dist/docs-site/00-quick-start-project-layout.html` still documents `tests/database-setup.sql` and a `data/` description that no longer matches.
