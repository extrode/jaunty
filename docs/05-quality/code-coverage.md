# Code Coverage

## Running it

```powershell
pwsh -NoProfile -File scripts/coverage.ps1                       # every suite under tests/
pwsh -NoProfile -File scripts/coverage.ps1 -Suite Jaunty.Tests   # one suite
```

Parameters: `-Suite`, `-Configuration` (default `Release`), `-Framework` (default `net10.0`).

Reports land in `tmp/coverage/<suite>/` as cobertura XML. The directory is gitignored, nothing is
deleted, and re-runs overwrite. Each suite's target directory is cleared first, because
`dotnet test` writes into a fresh GUID subdirectory per run and a stale one would be aggregated
twice.

The script warns when the number of cobertura files does not match the number of suites that ran.
That case matters: **a suite that passes while emitting no report reads as uncovered product
code**, which looks like a coverage problem and is a tooling problem.

The four `samples/torture-test-*` ports are out of scope by design. They sit outside
`Jaunty.slnx`, are not part of `dotnet test Jaunty.slnx`, and carry no collector.

## coverage.runsettings

Coverage settings live in `coverage.runsettings` at the repository root. They are applied by
`scripts/coverage.ps1` and **not** by an ordinary `dotnet test` run.

```xml
<ExcludeByAttribute>Obsolete,GeneratedCodeAttribute</ExcludeByAttribute>
```

That one line is the reason the file exists. Coverlet's own default also excludes
`CompilerGeneratedAttribute`, which hides lambda and closure bodies. Measured on
`Jaunty.FlatFiles.Tests` with `coverlet.collector` 10.0.1, the same run reports:

| `ExcludeByAttribute` | Classes | State machines | Lines |
|---|---:|---:|---:|
| `Obsolete,GeneratedCodeAttribute` (what we use) | 583 | 240 | 38,130 |
| plus `CompilerGeneratedAttribute` | 485 | 240 | 35,652 |
| **hidden by the wider exclusion** | **98** | **0** | **2,478** |

On this collector version async and iterator state machines are unaffected either way. What the
wider exclusion hides is 77 `<>c__DisplayClass` closures, 18 `<>c` lambda caches and 3
option/payload types - 2,478 lines of predicate and callback bodies reading as "not there".
`GeneratedCodeAttribute` is what the source generator stamps on emitted code, and that genuinely
should not be measured.

Every project under `tests/` already references `coverlet.collector` 10.0.1, so no project file
needs changing to be included.

---

## The 2026-08-27 baseline

Measured across 8 suites and 8,471 tests, 0 failures. Reports were union-merged per `(file, line)`,
with nested compiler-generated classes folded into their outer class and partial classes keyed by
name - both aggregation traps that inflate the numbers if skipped.

**Product total: 88.8 % line coverage over 25,469 lines, with 1,128 units of cyclomatic complexity
that no test executes.**

| Assembly | Cx | Lines | Cov % | Uncovered Cx |
|---|---:|---:|---:|---:|
| Jaunty | 6950 | 12,895 | 89.2 | 746 |
| Jaunty.Fluent | 1897 | 5,812 | 87.0 | 251 |
| Jaunty.Extensions.Reflection | 420 | 1,411 | 86.0 | 73 |
| Jaunty.Scaffolding | 465 | 1,521 | 81.9 | 31 |
| Jaunty.SourceGenerator | 205 | 1,099 | 91.5 | 18 |
| Jaunty.FlatFiles.DuckDB | 168 | 2,072 | 94.5 | 8 |
| Jaunty.FlatFiles | 64 | 358 | 99.2 | 1 |
| Jaunty.Scaffolding.Cli | 4 | 301 | 98.7 | 0 |

Ranking by *uncovered complexity* rather than by percentage is the point of the exercise: a class
at 60 % with two branches is not a target, and a class at 85 % with 232 units of complexity is.

## What the baseline found

The top-ranked class, `Internals.Parameters.SqlParameterParser` at 53.3 %, turned out not to be a
testing gap at all. The parser ships **twice**: `ExtractParameterNamesSpan` behind
`#if NET8_0_OR_GREATER`, and `ExtractParameterNamesClassic`, a hand-maintained duplicate compiled
into every target but only *called* on netstandard2.0 - the assembly .NET Framework consumers
load.

| Implementation | Lines | Covered | Rate |
|---|---:|---:|---:|
| `…Span`, live on net8.0/net10.0 | 81 | 81 | 100 % |
| `…Classic`, live on netstandard2.0 | 80 | 3 | 3.8 % |

`Jaunty.Tests` does target net472 and 50 parser tests pass there locally. But `ci.yml` is
`ubuntu-latest` throughout and **has no net472 leg**, so no CI run has ever executed the code path
.NET Framework users get. Two hand-maintained parsers that have to agree, one of them tested only
when someone happens to run the net472 leg on a Windows box, is a silent wrong-parameter-set
waiting to ship.

That is the shape of finding a coverage×complexity pass is for: the number pointed at a class, and
the reason behind the number was not the one the number implied.

---

## Coverage is not the gate

Line coverage says a line ran, not that anything asserted on what it did. Where coverage is
already high, the deficit is usually the oracle rather than the input generation, and mutation
testing is what shows it. Stryker configuration lives per test-project directory
(`tests/*/stryker-config.json`), scoped by the ranking above rather than run whole, with
`break: 0` - the score is an artifact to read, not a build gate.

**Do not run the full mutation tier on a development machine.** The measured runs and where they
belong are in [`../03-development/ci-architecture.md`](../03-development/ci-architecture.md).

The full testing strategy, its phases and its measurements are in
[`../plans/2026-08-27-003-testing-strategy-implementation.md`](../plans/2026-08-27-003-testing-strategy-implementation.md).

---

## See Also

- [`README.md`](README.md) - the quality section index
- [`../00-quick-start/build-and-test.md`](../00-quick-start/build-and-test.md) - the ordinary build and test loop
- [`../03-development/ci-architecture.md`](../03-development/ci-architecture.md) - where each tier runs
- [`../03-development/multi-targeting.md`](../03-development/multi-targeting.md) - why netstandard2.0 code needs its own leg
