# AGENTS.md

Engineering conventions for this repository, for humans and AI tools alike. This
governs internal workflow, not who may contribute — see [CONTRIBUTING.md](CONTRIBUTING.md)
for that: contributions require a signed CLA and must be authored by a human
(AI-generated contributions are not accepted, [CLA.md](CLA.md) Section 6).

## Git

- `main` = release, `dev` = integration. Never commit directly to either.
- Branch off `dev`: `feat/`, `fix/`, `chore/`, `docs/`, `refactor/`, `test/` prefixes.
- Merge back into `dev` with `--no-ff`.
- Commit prefixes match branch prefixes: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`.
- Pushing `dev` to `origin`, and releasing to `main`, is a maintainer decision.
- `dev` is protected: a PR and a passing `Build & Test` status check are required.
  Only the repo owner can bypass this; nobody else can push directly.

## Build

- `scripts/build.ps1` — restore, build (`dotnet build Jaunty.slnx -c Release`),
  run the net8.0/SQLite test slice.
- `scripts/build-aot.ps1` — NativeAOT publish of the Scaffolding CLI, output to
  `./publish-aot/` (gitignored).
- `scripts/build-docs.sh` — renders the docs site into `dist/docs-site/`, which
  is committed (everything else under `dist/` is gitignored).

## Testing

- Every change carries tests in the same branch — no "add tests later."
- Run the full suite before merging: `dotnet test Jaunty.slnx`.
- `docs/laws/` holds invariants that must hold before merge, each proven by a
  tagged test.
- Any reflection call added under `src/` needs an `AOT-SAFE` justification
  comment — `scripts/Verify-NativeAOT.ps1` fails CI otherwise.

## CI

- `.github/workflows/ci.yml` is the fast gate on every push and PR: build + full
  test suite across target frameworks.
- `.github/workflows/nightly.yml` runs the slow tier once a day: Stryker mutation
  testing, fuzzing, BenchmarkDotNet. Mutation score is a trend to read, not a
  merge gate.

## Docs

- `docs/` is numbered by area (`00-quick-start` through `08-learn`); see
  [docs/README.md](docs/README.md) for the index. `docs/99-archive/` is frozen
  history — don't edit it to fix a broken link, update the citing side instead.
- Before writing code or docs, check
  [api-design-guidelines.md](docs/03-development/api-design-guidelines.md) and
  [code-review-checklist.md](docs/03-development/code-review-checklist.md).
- `CHANGELOG.md` tracks user-facing changes; update it alongside a release-worthy
  change.

## File naming

kebab-case for new files, except `README.md`, `LICENSE.md`, `CHANGELOG.md`,
`AGENTS.md`, and language conventions (C# PascalCase types, etc).
