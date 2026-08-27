# SQLite.Interop for macOS arm64

`System.Data.SQLite.Core` ships native binaries for `win-x86`, `win-x64`, `linux-x64`
and `osx-x64` only — and the osx-x64 one is plain x86_64 Mach-O, so it cannot be
loaded into an arm64 process. 1.0.119 is the newest release, so there is nothing to
upgrade to.

On Apple silicon that means every test touching `System.Data.SQLite` fails with
`DllNotFoundException`. Measured on 2026-07-29: **1,099 of 1,099** failures in
`Jaunty.Tests` and all 887 in `Jaunty.Fluent.Tests` had this single cause.

```sh
export DOTNET_ROLL_FORWARD=LatestMajor   # only if no 8.0 runtime is installed — see below
./build.sh          # once per machine, ~1 min
dotnet build tests/Jaunty.Tests tests/Jaunty.Fluent.Tests
```

`MangleMap` targets `net8.0`, so on a machine with only the 10.0 runtime `build.sh` stops with
`You must install or update .NET to run this application. Framework: 'Microsoft.NETCore.App',
version '8.0.0'`. `DOTNET_ROLL_FORWARD=LatestMajor` runs it on 10.0 rather than installing a
second runtime for one helper. Measured 2026-08-27 on macOS 26.5.2 / SDK 10.0.400: 193 aliases
recovered from 203 mangled imports, the 3 unresolvable ones exactly the `sqlite3_win32_*` below.

The first suite run after a fresh build is slow — 10 m 14 s against 17.7 s of summed test time,
macOS verifying the new unsigned dylib. It is one-time; subsequent runs were 13 s.

After that both suites pass locally: `Jaunty.Tests` 2,763 passed / 0 failed
(2,447 skipped for want of a live SQL Server or PostgreSQL), `Jaunty.Fluent.Tests`
1,182 / 0.

`SQLiteInteropOsxArm64.targets` copies the result into the two test projects that
need it. It is conditioned on the SDK's own RID and on the artifact existing, so CI,
Linux, Windows and Intel Macs are unaffected, and nothing reaches any package. The
artifact is gitignored — it is a build output, and a checked-in binary nobody can
reproduce is worse than a script.

## Why this is not just a compile

Two things make it more than running the vendor's own
`Setup/compile-interop-assembly-release.sh` with `-arch arm64`.

**The shipped managed assembly imports mangled names.** Its P/Invokes reference
`SI` + 16 hex digits, not `sqlite3_*`. The mangling is applied by the vendor's build
after the fact and is not reproducible from the published source, so `MangleMap`
recovers the mapping instead: the assembly's P/Invoke table gives
*managed method → mangled name*, and `UnsafeNativeMethods.cs` gives
*managed method → real entry point*. The two join on the managed method name, and the
result is applied as linker aliases.

That map is derived from the installed package on every run, so bumping
`System.Data.SQLite.Core` self-corrects rather than silently producing a library
that cannot bind. It cross-checks against the vendor's own binary: 200 of the 203
mangled imports are exported by the shipped osx-x64 native, and the 3 that are not
are exactly the three `sqlite3_win32_*` the script also reports as unresolvable.

**`sqlite3_config` and `sqlite3_db_config` are variadic, and System.Data.SQLite
P/Invokes them with fixed signatures.** On x86-64 that mismatch is invisible — a
variadic callee reads its arguments from the same registers a fixed-signature caller
puts them in. On Apple arm64 it is fatal: a variadic callee reads variadic arguments
off the stack while a fixed-signature caller passes them in `x1..x7`.

The failure does not look like an ABI problem. The config call appears to succeed;
SQLite stores a garbage pointer for `SQLITE_CONFIG_LOG` and jumps through it the
first time it logs anything — here the `SQLITE_WARNING_AUTOINDEX` that
`constructAutomaticIndex()` emits on a multi-table join. The process dies with
`SIGBUS` inside `sqlite3_prepare`, nowhere near the mistake, and only on some
queries. `src/arm64-varargs-shim.c` fixes it with fixed-signature wrappers that
forward each configuration verb with the argument shape it actually expects; the
mangled names are aliased onto those instead of onto the real variadic functions.

This is very likely why no osx-arm64 build exists upstream.

## Layout

| | |
|---|---|
| `build.sh` | downloads the source drop, compiles, derives the aliases, links, verifies |
| `src/arm64-varargs-shim.c` | fixed-signature wrappers for the two variadic config entry points |
| `src/codec-stubs.c` | `sqlite3_key`/`sqlite3_rekey`, which the stock amalgamation omits without SEE |
| `MangleMap/` | recovers mangled → real entry-point names from the installed package |
| `SQLiteInteropOsxArm64.targets` | copies the artifact into test output on osx-arm64 only |
| `artifacts/` | build output, gitignored |

## Scope

This exists so the suite is runnable on an arm64 Mac. It does not make Jaunty
support macOS arm64 for `System.Data.SQLite` — consumers on Apple silicon hit the
same missing native and would need the same workaround. CI remains the authority for
`System.Data.SQLite` behaviour.
