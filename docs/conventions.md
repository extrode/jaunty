# Conventions — jaunty

Mechanical how-we-work rules. Defaults are project-local;
this file records project-specific deltas. Binding principles live in docs/constitution.md;
project memory lives in docs/lessons/.

## File & directory naming
- kebab-case for files and dirs: `mail-pipeline.md`, `data-model.sql`.
- Except where an established convention dictates otherwise: `README.md`, `CHANGELOG.md`,
  `LICENSE.md`, `Directory.Build.props`, or language-mandated casing (C# `PascalCase.cs`).
- Unsure? Match the nearest existing sibling file.

## Git workflow
- `main` is release-only; `dev` is integration. Never commit directly to either.
- All work on named branches off `dev` (`feat/ fix/ chore/ docs/ refactor/ test/`). Commit often.
- Merge completed branches back to `dev` with `--no-ff`. Always merge once a branch is complete —
  never leave finished work unmerged.
- Release: `dev` -> `main` `--no-ff` + semver tag. Ask before deleting branches.

## Cleanup policy
- NEVER auto-delete files, branches, or artifacts (the guards block it anyway).
- Instead: append every cleanup candidate to a running list, then generate a reviewed PowerShell
  script at `scripts/cleanup-<milestone>.ps1` for the user to run manually (flat in `scripts/`,
  alongside any existing build/publish scripts — no `cleanup/` subfolder).
- Script rules: `$ErrorActionPreference='Stop'`; safety gate (refuse if not on `dev`); numbered
  sections; `git branch -d` (never `-D`); destructive/DB steps commented-out or opt-in; print the
  remaining state at the end. Follow the existing `cleanup-*.ps1` pattern in the repo.

## Specs freshness
- When code diverges from a shipped spec, update `docs/specs/NNN-*/spec.md` (bump its `Status:`
  and add a `last-verified: <date>` line). `/spec-status` flags any spec older than its own
  plan/tasks as stale.
