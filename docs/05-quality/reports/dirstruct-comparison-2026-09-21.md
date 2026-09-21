# Directory structure comparison: Jaunty vs JauntyQ (2026-09-21)

Raw ASCII trees (generated + committed for reference):
- `dirstruct-jaunty-2026-09-21.txt`
- `dirstruct-jauntyq-2026-09-21.txt`

Both generated the same way: `find`, pruning `.git/bin/obj/node_modules/StrykerOutput/.vs/tmp/TestResults/dist`,
plus JauntyQ-specific `worktrees/` (a leftover `.claude` agent worktree that duplicated the entire
tree at `.claude/worktrees/agent-a15d2faca51591fc4/...` — not real structure, excluded).
`artifacts/` is gitignored in both repos (`artifacts/` line in each `.gitignore`) and holds only
generated coverage GUIDs / test-result dumps in JauntyQ's copy — noise, not structure; ignore it
in the comparison below even though the tree file still lists it.

## Top level

| Jaunty | JauntyQ | Note |
|---|---|---|
| `.claude`, `.github`, `benchmarks`, `docs`, `samples`, `scripts`, `src`, `tests`, `tools` | same | consistent already |
| `.config` (dotnet-tools.json) | *(absent)* | JauntyQ has no local tool manifest — either doesn't need one yet, or an oversight |
| `data/` — 4 schema JSON fixtures (edgecases/northwind/relationships) | `data/` — **empty dir**, no files | same name, different (and in JauntyQ's case, unused) purpose |
| `seed/` — mariadb/mssql/mysql/postgres/sakila, engine-first | *(absent — folded into each sample's own `db/`)* | two different fixture-organization philosophies, see below |
| *(absent)* | `test-infra/` | JauntyQ has a dedicated shared-test-infrastructure root; Jaunty's equivalent content lives inside `tests/Helpers/` |

## Fixture/seed data: two incompatible philosophies

- **Jaunty**: engine-first, centralized — one `seed/<engine>/` and `data/` tree shared across all
  test projects that need it.
- **JauntyQ**: sample-first, colocated — every `samples/<Sample>.Tests/db/{schema,tables/<Table>}/`
  carries its own fixture, duplicated per dialect (e.g. the Conduit schema exists once each under
  `.MariaDb.Tests`, `.MySql.Tests`, `.Postgres.Tests`, `.SqlServer.Tests`, `.Sqlite.Tests` — five
  near-identical `db/tables/{ArticleTags,Articles,Comments,Favorites,Follows,Tags,Users}` copies).

These aren't a small naming quirk — they're opposite answers to "where does fixture data live,"
and picking a common-ground convention means deciding which philosophy wins (or an explicit split:
central corpus for raw seed SQL, per-project generated model dirs for scaffolded output). Flagging
this as the one structural decision in this report that isn't just renaming.

## `docs/`

| Jaunty | JauntyQ |
|---|---|
| `00-quick-start` | `00-overview` |
| `01-api-reference` | `01-getting-started` |
| `02-architecture` | `02-learn` |
| `03-development` | `03-guides` |
| `04-extensions` | *(missing — no 04)* |
| `05-quality/reports` | *(missing — no 05)* |
| `06-releases` | `06-reference` |
| `07-design` | `07-roadmap` |
| `08-learn/migrating` | *(missing — no 08)* |
| `99-archive/...` (deep history, 2026-01 through 2026-03) | *(absent — no archive at all)* |
| `_assets/{benchmarks,logo,screenshots}` | `_assets/logo` **and** a separate `assets/` (one file, `build-not-prod.svg`) |
| `architecture/` — **empty except `.gitkeep`** | *(absent)* |
| `benchmark-artifacts/results` | *(absent — benchmark output presumably lives under gitignored `artifacts/`)* |
| `decisions/`, `lessons/`, `plans/`, `specs/` | same four, same names |
| `laws/` (3 laws + README, each with a matching `tests/.../Unit/Laws/L00N*Tests.cs`) | *(absent — no law/invariant-doc convention)* |
| *(absent)* | `handoffs/` |

Findings:
- **Same numeric prefixes, different topics.** `02-` is architecture in Jaunty, "learn" in JauntyQ;
  `06-`/`07-` also point at different content. A numbered-docs convention only pays off if the
  numbers mean the same thing everywhere — right now they're coincidentally numbered, not aligned.
- **`docs/architecture/` in Jaunty is dead** — just a `.gitkeep`, nothing else. Looks like a
  half-finished migration away from (or into) `02-architecture`. Candidate for deletion.
- **`docs/assets/` vs `docs/_assets/` in JauntyQ is a duplicate** — one real asset sitting outside
  the underscore-prefixed convention the rest of the repo (and Jaunty) uses. Candidate to merge
  into `_assets/`.
- Jaunty's `laws/` + matching `Unit/Laws/L00N*Tests.cs` pattern is a genuinely nice invariant-doc
  convention with nothing to compare against in JauntyQ — not an inconsistency to resolve, just
  worth deciding whether JauntyQ wants the same pattern later.
- JauntyQ's `handoffs/` has no Jaunty counterpart — likely fills a role Jaunty doesn't formalize
  (session handoff notes); worth checking whether Jaunty needs the same thing or already covers it
  elsewhere (e.g. this very memory system).
- Jaunty keeps a full `99-archive/` of dated historical docs; JauntyQ has none — could mean JauntyQ
  is younger and hasn't accumulated archive material yet, not necessarily a gap.

## `src/`

Both use the same convention and it's clean: `src/Extrode.<Product>[.<Feature>]/<PascalCase folders
by concern>`. No inconsistency worth flagging here — this is the strongest existing common ground
and should be the anchor for any unified convention, not something that needs to change.

One packaging-only difference: JauntyQ's `Extrode.JauntyQ.Generator` carries `build/` and
`buildTransitive/` folders (standard NuGet analyzer-packaging convention for `.props`/`.targets`
assets). Jaunty's `Extrode.Jaunty.SourceGenerator` has neither — worth a quick check (out of scope
for this report) on whether Jaunty's source generator package actually needs the same MSBuild
props/targets plumbing or genuinely doesn't.

## `tests/`

- **Naming pattern mismatch inside Jaunty itself** (not just vs. JauntyQ): most Jaunty test
  projects are `Extrode.Jaunty.<X>.Tests`, but two break the pattern —
  `Extrode.Jaunty.UnitTests` (no dot before "Unit") and the bare `Extrode.Jaunty.Tests` (covers
  both core + the Reflection extension, ambiguous from the name alone). JauntyQ's test projects are
  uniformly `Extrode.JauntyQ.<X>.Tests` with no exceptions.
- **Redundant nesting**: `Extrode.Jaunty.UnitTests/Unit/...` — the project name already says "Unit"
  and then nests everything one more level under a folder also called `Unit/`. None of JauntyQ's
  test projects double up like this.
- **Inconsistent internal organization even within Jaunty**: some test projects split top-level by
  test type (`Unit/`, `Integration/`, `Performance/` — e.g. `Extrode.Jaunty.Tests`,
  `Extrode.Jaunty.FlatFiles.DuckDB.Tests`), others mix type-based folders with bare
  concern-based folders at the same level (`Extrode.Jaunty.Fluent.Tests` has `Unit/`, `Integration/`
  *and* bare `Entities/`, `Helpers/`, `data/` siblings). JauntyQ's test projects are flatter overall
  (most have no `Unit`/`Integration` split at all — just concern folders, or nothing beneath the
  project root), so there's no direct precedent to copy; this is really a Jaunty-internal
  consistency gap to fix on its own.
- JauntyQ has no equivalent of Jaunty's `Helpers` top-level `tests/Helpers` (cross-project test
  helpers) — everything shared instead lives in the top-level `test-infra/` noted above.

## `scripts/`, `tools/`

- Both have `scripts/cleanup` — consistent.
- Jaunty additionally has `scripts/tests`; JauntyQ has `scripts/repo-split` — different one-off
  needs, not an inconsistency (no reason to force parity here).
- `tools/Extrode.<Product>.Fuzz/corpus` — identical convention in both. Good anchor.
- Jaunty additionally has `tools/native/sqlite-interop-osx-arm64/...` — genuine extra need
  (native interop build), not present in JauntyQ and not expected to be.

## Proposed common ground

1. **`src/` layout is already aligned — freeze it as the standard**: `src/Extrode.<Product>[.<Feature>]/<Concern>/`.
2. **Fix Jaunty's own test-project naming first**, independent of JauntyQ: rename
   `Extrode.Jaunty.UnitTests` → fold into `Extrode.Jaunty.Tests` or rename to
   `Extrode.Jaunty.Unit.Tests`; consider whether the bare `Extrode.Jaunty.Tests` should be split so
   its name isn't ambiguous about covering the Reflection extension too. Then require
   `Extrode.<Product>.<Feature?>.Tests` uniformly on both repos going forward — JauntyQ already
   complies.
3. **Standardize the docs numbering** across both repos with one shared table of what each prefix
   means (something like `00-overview`, `01-getting-started`, `02-architecture`, `03-development`,
   `04-extensions`(if applicable), `05-quality`, `06-reference`, `07-roadmap/design`,
   `08-learn`(if applicable), `99-archive`), rather than letting each repo assign topics to numbers
   independently. Both repos migrate to the same map; folders that don't apply to a given repo are
   simply omitted, not renumbered.
4. **Delete Jaunty's dead `docs/architecture/`** (`.gitkeep` only) and **merge JauntyQ's stray
   `docs/assets/build-not-prod.svg` into `docs/_assets/`** — two small dead-weight removals, no
   design decision required.
5. **Decide the fixture-data philosophy** (the one real architectural choice here): either keep
   Jaunty's centralized `seed/<engine>/` approach and have JauntyQ adopt it for shared corpora,
   or keep JauntyQ's per-sample `db/{schema,tables/<Table>}` colocation and have Jaunty move its
   `seed/`+`data/` content next to the projects that consume it. A hybrid is also reasonable
   (shared raw SQL corpora centralized, generated/scaffolded per-sample output colocated) — this
   needs your call, not something to guess at.
6. **Optional, lower priority**: consider whether Jaunty wants a `laws/` + `Unit/Laws/` invariant
   convention pulled into JauntyQ, and whether JauntyQ's `handoffs/` doc convention is worth
   adopting in Jaunty (or is already redundant with this session's memory system).

No files were changed by this report — it's read-only analysis, per your framing ("come to a
common ground... once settled, we'll try to use this structure henceforth"). Items 4 and the
Jaunty test-naming fix in item 2 are cheap, no-judgment-call cleanups I can do immediately once
you confirm; item 5 needs a decision from you before anything moves.
