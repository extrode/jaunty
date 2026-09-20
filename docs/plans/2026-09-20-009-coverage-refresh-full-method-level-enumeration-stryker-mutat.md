# Coverage refresh (full method-level enumeration) + Stryker mutate-scope widening

## Context

The last coverage baseline (`docs/05-quality/code-coverage.md`, "2026-08-27 baseline") is
~3 weeks stale and predates the `Extrode.Jaunty.*` namespace rename. It reports assembly-level
line coverage only (88.8% over 25,469 lines). User wants it refreshed, and extended down to
file level and method level: every uncovered method listed, with a reason (real gap, dead
platform path, defensive/unreachable code, generated-code artifact, etc.) — same triage style
already demonstrated once in that doc for `SqlParameterParser` (looked like a 53% gap, was
actually a netstandard2.0-only duplicate parser CI never exercises).

Separately, this session found that Stryker's `mutate` scope only covers two of the ten `src/`
projects (`Extrode.Jaunty`: `Internals/Parameters/**` + `Dialects/**`; `Extrode.Jaunty.Fluent`:
`Expressions/**`), chosen by the same kind of uncovered-complexity ranking. User wants that
widened. Mutation testing is currently disabled in CI (`CI_MUTATION_ENABLED` unset, pending the
Blacksmith GitHub App activation — an existing owner blocker), so widening scope now is safe to
land without an immediate runtime cost; it takes effect whenever the owner flips that flag.

## Track 1 — Coverage refresh with method-level detail

1. Run `pwsh -NoProfile -File scripts/coverage.ps1` for all 10 suites under `tests/` (the script
   auto-discovers them; the 4 `samples/torture-test-*` ports stay out of scope as documented).
   This runs live-DB integration suites too, so expect it to take a while and require whatever
   local DB fixtures `Extrode.Jaunty.Tests` needs (SQLite works without setup; SQL
   Server/MySQL/Postgres suites skip cleanly if a container isn't up, same as the 2026-07-04
   report — I'll note which suites skipped, not treat their 0% as real).
2. Parse each suite's `tmp/coverage/<suite>/**/coverage.cobertura.xml`. Cobertura already reports
   per-`<method>` line-rate and hit counts, so method-level detail doesn't need re-deriving from
   raw line data — reuse that structure instead of re-inventing it.
3. Union-merge per `(assembly, file, method)` across suites, same two traps the 08-27 baseline
   called out: fold nested compiler-generated classes into their outer class, key partial classes
   by name so they don't double-count.
4. Produce three tables, ranked by uncovered complexity (not raw %, per the existing doc's own
   reasoning):
   - Assembly level (refresh of the existing table)
   - File level within each assembly
   - Method level: every method at 0% or partial coverage, file:line, complexity
5. Per uncovered method, determine *why*. Given the likely volume (the 08-27 baseline had 1,128
   uncovered-complexity units across 8 assemblies), this is delegated per-assembly to up to 3
   concurrent `Agent` dispatches (general-purpose, read-only: Read/Grep/Glob), each handed its
   assembly's parsed uncovered-method list directly in the prompt (not told to re-run coverage or
   re-parse XML) and asked to classify each: genuine gap / platform-conditional dead path (like
   the netstandard2.0 parser) / defensive branch that can't be hit / generated code miscounted /
   tested only transitively and worth double-checking. I synthesize their output into the report
   rather than trusting it uncritically — spot-check a sample against source before writing it up.
6. Write `docs/05-quality/reports/coverage-gaps-2026-09-20.md`, matching the existing
   `coverage-gaps-2026-07-04.md` format (per-assembly table, prioritized genuine-gap list,
   "0% only because X" category, reproduction command). Update `code-coverage.md`'s baseline
   section to point at the new dated report instead of carrying stale inline numbers.
7. Branch `docs/coverage-refresh-2026-09-20` off `dev`, commit, merge `--no-ff`.

## Track 2 — Stryker mutate-scope widening

1. Use Track 1's file-level ranking to pick which currently-unmutated projects/folders get a
   `stryker-config.json` (mirrors how `Internals/Parameters`+`Dialects` and `Expressions` were
   originally chosen — by ranking, not by mutating everything).
2. Add `stryker-config.json` (same shape as the two existing ones: `mtp` runner, `progress/html/json`
   reporters, `thresholds: {80,60}`, `break: 0`, `concurrency: 4`) for the projects currently
   absent from the nightly matrix: `Extrode.Jaunty.Extensions.Reflection`,
   `Extrode.Jaunty.Scaffolding`, `Extrode.Jaunty.SourceGenerator`, `Extrode.Jaunty.FlatFiles`,
   `Extrode.Jaunty.FlatFiles.DuckDB`, `Extrode.Jaunty.Scaffolding.Cli` — scoped to each one's
   highest-uncovered-complexity folder(s) from Track 1, not their whole `src/` tree, consistent
   with the "scoped by the ranking... rather than run whole" convention already documented.
3. Add matching entries to the `mutation` job's matrix in `.github/workflows/nightly.yml`. Stays
   inert until `CI_MUTATION_ENABLED == 'true'` (existing gate, owner-controlled).
4. Check whether `Extrode.Jaunty.Fluent.Tests` needs the same `--test-project` cross-project fix
   already applied to `Extrode.Jaunty.UnitTests` this session (the nightly.yml comment on that
   `include:` block explicitly flags this as unanalyzed). Grep for tests of
   `Extrode.Jaunty.Fluent`'s `Expressions/**` living in a different test project; if found, add
   the same `extra-test-project` matrix entry. If not, leave as-is and don't guess.
5. Do **not** widen the two already-enabled projects' mutate globs from their current targeted
   subfolders to their full `src/` tree — that multiplies the mutant count for jobs that already
   run once the flag is on, and the cost is unmeasured. Flag this as a follow-up decision point in
   the commit/doc, not something to do unilaterally.
6. Branch `chore/stryker-scope-widen` off `dev`, commit, merge `--no-ff`. No live Stryker run
   needed for this track — the configs are inert until the owner enables the CI flag, so there's
   nothing to execute or verify beyond `dotnet build` still succeeding (config files don't affect
   the build) and the YAML being valid.

## Verification

- Track 1: `scripts/coverage.ps1` exits 0 for all suites that have working local fixtures; the
  script's own cobertura-count-vs-suite-count check catches any suite that silently produced no
  report. Cross-check a handful of the "genuine gap" classifications by reading the actual method
  before publishing.
- Track 2: `.github/workflows/nightly.yml` stays valid YAML (`actionlint` if available, otherwise
  visual diff review); `dotnet build Jaunty.slnx -c Release` still passes (config-only change,
  shouldn't affect it, but confirms nothing else broke).
- Both merge into `dev`, unpushed, per standing convention.
