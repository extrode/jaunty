# Audit record

Jaunty has been under a standing internal audit since 2026-07-04. This page is the public
summary. The working record — per-round finding files, the findings registry, the carry-forward
lists and the coverage ledger — is kept in a private repository.

## Scope and method

| | |
|---|---|
| Rounds | 36, numbered `round2` to `round36`; 33 with their own directory |
| Span | 2026-07-04 to 2026-08-26 |
| Findings with an identifier | 725 (`AUD-R<round>-<n>`) |
| Registry | a single append-only file, 1.05 MB |

Each round takes a slice of `src/` and reads it against the constitution in
`docs/constitution.md` — performance in hot paths, NativeAOT compatibility, dialect correctness,
and API shape. A round ends by writing down what it did **not** fix and why; the next round
inherits that list. Findings are recorded whether or not they were acted on, including the ones
judged false on a second reading, so that a later round does not re-file them.

One thing follows from that method and is worth stating plainly:

- **A finding in the registry is a claim, not a verdict.** Some were wrong. Where a round's
  conclusion was later overturned by running the code, the reversal is recorded next to it.

## Security findings

Eight families of security-relevant findings were raised across the 36 rounds. **All eight are
closed in `src/` as it stands.** Each was re-verified on 2026-08-29 by reading the current code
rather than by trusting the registry entry that claimed the fix.

| # | Family | Highest severity | Where it is closed |
|---|---|---|---|
| 1 | CSV import: SQL injection, and command execution through the `sqlite3` CLI | Critical | `ValidateIdentifier` plus per-dialect `EscapeTableName`/`EscapeColumnName`, at all 7 call sites in `Import/CsvImport.cs` |
| 2 | Dialect identifier escaping — raw interpolation of table and column names | High | All 5 files in `src/Jaunty/Dialects/` route through `SqlIdentifierValidator` |
| 3 | Bulk copy identifier injection | High | All 3 providers escape or validate |
| 4 | FlatFiles / DuckDB identifier and literal injection | High | `SanitizeTableName` on every filename-derived table name; `EscapeStringLiteral` doubles `'` |
| 5 | `CteBuilder` — CTE name written to SQL unvalidated | High | `SqlIdentifierValidator.Validate` at `CteBuilder.cs:34`, before the name reaches any SQL |
| 6 | `JoinExpressionVisitor` — values inlined rather than parameterized | High | Parameterized |
| 7 | `LoggingInterceptor` — parameter values logged unmasked | High | Gated by `IsSensitiveParameter`; the class now lives in `Jaunty.Extensions.Logging` |
| 8 | `CommandContext` — connection string exposed with credentials | Medium | `SanitizeConnectionString`, via `DbConnectionStringBuilder` |

The full reasoning is in `docs/plans/2026-08-29-005-open-findings-classification.md`, which is
public.

## What remains open, and why it is not published

What the audit still carries forward is correctness, test coverage, API-shape decisions and
process — no open item is security-relevant, by the check above. The carry-forward lists are
nonetheless kept private. They are, by construction, a ranked and `file:line`-indexed list of
defects a maintainer chose not to fix yet, and a defect in a data-access library can become a
security defect in the application built on it. Publishing the list would hand an attacker a
prioritised worklist and save them the audit.

This is a deliberate asymmetry and not a claim that the code is free of defects. If you find one,
`SECURITY.md` says how to report it.

## Fixes that were later reworked

A finding's fix is not frozen. Where a later change kept a finding's guarantee but replaced the
mechanism behind it, the change is listed here so that a future round reading the registry entry
does not restore the old mechanism as a regression fix.

| Finding | What it guaranteed | What changed, and where it is recorded |
|---|---|---|
| AUD-R35-069 | The generated mapper throws `Cannot assign NULL to non-nullable property 'X'.` for a NULL in a non-nullable value type, matching the reflection mapper | 2026-09-02: the per-column `IsDBNull` pre-check that produced it was replaced by a `try` around the row's reads and a `catch` that names the column. Same exception, same message, provider exception as `InnerException`. Reason and evidence in [decision 010](../decisions/2026-09-02-010-null-guard-by-catch-not-precheck.md); pinned by `GeneratedNullDiagnosisTests` |

## Citations elsewhere in this repository

Specs and plans written while the audit directory was tracked cite it directly — paths of the
form `audit/findings-registry.md` or `audit/round26/03-findings.md`, and identifiers of the form
`AUD-R25-030`. Those documents are records of what was decided at the time and have been left as
written rather than rewritten to hide a path.

**The identifiers still resolve; the paths no longer do.** Both refer to the private record. Three such citations remain, in `docs/specs/008-dialect-parameter-binding/008-spec.md`,
`docs/specs/009-aot-annotation-pass/009-spec.md` and `docs/specs/010-net10-migration/010-tasks.md`.

## Reporting

Security issues: see [SECURITY.md](../../SECURITY.md). Everything else: open an issue.
