# Jaunty.Fuzz

Coverage-guided fuzzing of `SqlParameterParser.ExtractParameterNames` via
[SharpFuzz](https://github.com/Metalnem/sharpfuzz) 2.3.0 and libFuzzer.

## Why this target

`ExtractParameterNames` runs on every parameterised query Jaunty executes, unguarded. It is a
hand-written span walker over semi-trusted text with a genuine state machine — string literals,
doubled quotes, backslash escapes, quoted identifiers in three flavours, line and block comments,
`@@` system variables, Postgres dollar-quoting — which is the shape the strategy handover's gate
names as worth fuzzing. The 2026-08-27 mutation baseline scored this file **100%** (312 killed,
0 survived, 0 NoCoverage), so the remaining risk is not a weak oracle but an input nobody thought
to write down.

## Oracle

Total-ness plus two shape invariants. The harness fails when the parser:

- throws anything on any byte sequence that decodes as UTF-8;
- returns an empty parameter name;
- returns a name containing a character `IsParameterChar` rejects.

The last two matter because a returned name is used to look up a property and then to build a
`DbParameter` — a malformed name fails much later, with nothing pointing back at the SQL.

Both escape modes are driven from every input. `backslashEscapes: true` is the MySQL/MariaDB
rule and a different state machine over the same text; a crash reachable in only one mode is
still a crash.

## Running it

Linux only — libFuzzer has no Windows driver. The self-hosted runner (`vars.CI_RUNNER`) is
Linux, so the nightly workflow executes this rather than only building it.

```sh
dotnet publish tools/Jaunty.Fuzz -c Release -o out/fuzz
sharpfuzz out/fuzz/Jaunty.dll                      # instrument the library, not the harness
./libfuzzer-dotnet -max_total_time=600 \
  --target_path="$(command -v dotnet)" \
  --target_arg=out/fuzz/Jaunty.Fuzz.dll \
  tools/Jaunty.Fuzz/corpus
```

The last line must go through the native `libfuzzer-dotnet` driver. Launched as a plain
`dotnet Jaunty.Fuzz.dll`, SharpFuzz finds none of the IPC environment variables and replays
`args[1]` as a single input file instead of fuzzing — it exits 0 and looks like a clean run.

## Corpus

`corpus/` holds 20 hand-written seeds, one construct each: the three sigils, decoys inside
literals and comments, doubled quotes, all three quoted-identifier styles, unterminated
comments and literals, `@@` variables, dollar-quoting tagged and untagged, a backslash escape,
a sigil inside an identifier, and four degenerate inputs (empty, lone sigil, sigil run, quote
run).

## When it finds something

Minimise first (`-minimize_crash=1`), then promote the minimised input into
`ParameterParserPropertyTests` as a named case with the reason it was kept. A crasher that
lives only in `corpus/` is a finding nobody re-checks.
