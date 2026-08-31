# Open audit findings: security or correctness?

Written 2026-08-29, as one of the checks that had to pass before the repository was made public.

**Verdict: no open finding is security-relevant.** Every one of the eight security families the
audit ever raised is closed in `src/` today, verified against the code rather than against the
registry. What remains open is correctness, coverage, API-shape decisions and process — publishable
as-is.

---

## Why the question was asked

`audit/` is 1,188 tracked files; `findings-registry.md` alone is 1.05 MB. Grepping it for open-work
markers returns **108 lines** (`not fixed`, `still open`, `outstanding`, `unresolved`) plus 111 more
for `deferred` — the "51 + 48 open findings" the release plan estimated. Publishing a
severity-ranked, `file:line`-indexed list of *unfixed* defects in a data-access library is a gift to
an attacker, so the question is not "how many are open" but **"is any of them a way in"**.

## Method, and why the grep count is not the answer

The 108 marker lines are not 108 open findings. Sampling them, they are mostly:

| What the marker actually is | Example |
|---|---|
| A round narrating that it fixed something *else* | `no false positives left unresolved` |
| A cross-reference to an item already tracked elsewhere | `already tracked in carry-forward` |
| An item since closed, with the prose never updated | five of round 35's seventeen unticked lows were already fixed |
| A deliberate decision recorded as a non-fix | `Still open by design:` |

So the count was not the instrument. **Each round's `00-carry-forward.md` is the open work-list by
construction** — a round's close-out writes down what it did not fix and why, and the next round
inherits it. Reading the newest and following its "older lists still stand" pointers gives the
actual backlog:

- `audit/round36/00-carry-forward.md` (2026-08-26, the current one)
- `audit/round35/00-carry-forward.md`
- `audit/round30/00-carry-forward.md`
- `audit/round28/00-carry-forward.md` and the round-27 items it carries

Then, separately, the security families were checked **against `src/` as it stands**, because a
registry entry saying "fixed" is a claim and the code is the evidence.

## The eight security families, verified in code

Every security-classed finding the audit ever raised belongs to one of these. All eight are closed;
each row was confirmed by reading the current file, not the registry entry.

| # | Family | Where it stood | State in `src/` today |
|---|---|---|---|
| 1 | CsvImport SQL injection and `sqlite3` CLI command execution | CRITICAL ×2 | `ValidateIdentifier` plus per-dialect `EscapeTableName`/`EscapeColumnName`, 7 call sites in `Import/CsvImport.cs` |
| 2 | Dialect identifier escaping — raw interpolation of `[Table]`/`[Column]` and resolver output | HIGH | All **5** files in `src/Jaunty/Dialects/` route through `SqlIdentifierValidator`, which enforces `^[Letter_][LetterOrDigit_]*$` and throws otherwise |
| 3 | BulkCopy identifier injection | HIGH | All **3** providers escape or validate |
| 4 | FlatFiles / DuckDB identifier and literal injection | HIGH | `SanitizeTableName` on every filename-derived table name; `EscapeStringLiteral` doubles `'` |
| 5 | `CteBuilder` — CTE name written to SQL unvalidated | HIGH | `SqlIdentifierValidator.Validate(cteName, ...)` at `CteBuilder.cs:34`, before the name reaches any SQL |
| 6 | `JoinExpressionVisitor` — values inlined instead of parameterized | HIGH | Parameterized |
| 7 | `LoggingInterceptor` — parameter values logged unmasked | HIGH | `_config.IsSensitiveParameter(paramName)` gates the value; the class moved to `Jaunty.Extensions.Logging` in the zero-dependency work |
| 8 | `CommandContext` — raw connection string, credentials included | MEDIUM (consistency) | `SanitizeConnectionString` via `DbConnectionStringBuilder`, closed as AUD-R35-167 |

Family 4 has one **open** item, and it is worth stating precisely because it reads as security at a
glance and is not: `DuckDbDialect.EscapeStringLiteral` has **no direct test**. The method itself is
present and correct (`value.Replace("'", "''")`). The finding is a coverage gap, not a hole.

## What is actually open, and what class it is

| Class | Items | Publishable? |
|---|---|---|
| **Needs an owner decision** | Naming resolvers ignored on the generated path (3 rounds old); `QueryCore` ignores `MappingMode`/`options.Mapper`; `FlatFileOptions.Sources` public `List<>`; SQLite `decimal` into REAL affinity; batch-10 `CommandOptions` interface gaps (~60 methods); `DuckDbWrite` chunked inserts non-atomic | Yes — design questions, not defects |
| **Deferred with a stated reason** | `ParameterBinder` tokenizer duplication; `GroupedJoinedResultMapper` AOT pragmas (parked on spec 009); DuckDb.cs session-setup outside `CommandObservation`; hand-maintained `ci.yml`/`release.yml` project lists; `GroupByExpressionVisitor` refactor; DuckDB sync/async parameter-signature divergence | Yes |
| **Round-27 carry-forward (CF-1…CF-21)** | Precision past 2^53, dialect capability inheritance, two unbounded write-path caches, inert `BulkCopyIdentityMode.KeepIdentity`, AOT verifier allow-lists, projection rooting | Yes |
| **Coverage** | `QueryCore` (05c) and `DeleteCore` (05es1b) work-lists; arity-2/3 multi-entity; `ResolveSpecialTypeMapper` fail-fast branches; `DateTimeOffset` string branch; `TablePromoter` async scenarios; `DuckDbDialect.EscapeStringLiteral` | Yes |
| **Unfixed lows** | Round-27 batches 6/7/8 — collation and `ORDER BY` matching, allocation in logging, stopwatch scope, cosmetic error text | Yes |
| **Test infrastructure** | `SqlServerSchemaReaderTests` races itself across framework legs (fixed-name `scaffold_test_*` tables in one shared database). **CI is unaffected** — it runs the legs separately | Yes |
| **Security** | **none** | — |

## Two caveats that survive this classification

**1. Coverage claims from rounds 10–33 are not trustworthy.** SQL Server was dark for the whole
audit loop until round 34 — 2,447 tests in `Jaunty.Tests` ran for the first time mid-round-34. "There
is a test for it" from an earlier round may mean "there is a test that never ran", and with the
service down the suite silently skips and still reports green. Check the skip count, not the failure
count. This does not change the verdict above, which was checked against code, but it does mean the
*coverage* rows are softer than they look.

**2. The GA gate is unrelated to this and still open.** The audit loop's own rule is three
consecutive clean rounds; round 35 found real findings, so the counter is **0 of 3** and the gate is
round 38 at the earliest. Going public does not require it — this classification does.

## What this does not decide

`audit/` still moves to a private repository under **D3**, and that decision stands on its own: this
document says the open findings are safe to publish, not that 1,188 files of audit history are worth
publishing. The classification is what T20 needed; the move is what T1 tracks.

## See also

- `docs/03-development/continuous-audit.md` — the loop that produces new findings, and why raw
  reports are never committed
- `docs/05-quality/audit-record.md` — the public summary of the audit rounds behind this
  classification
