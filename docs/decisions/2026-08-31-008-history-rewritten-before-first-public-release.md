# The history was rewritten before the first public release

**Date:** 2026-08-31
**Status:** Decided and carried out by the owner, before the repository was made public.
**Supersedes nothing.** Closes T15 of `docs/plans/2026-08-29-004-public-release.md`.

## The decision

Jaunty's git history was rewritten in three passes, all of them **before** the repository was
published rather than after. The published history is therefore not the history the project was developed
against, and this record exists so that nobody has to guess why.

Three things changed:

- **147 commit subjects** were replaced with conventional-commit messages. They had been
  pasted-in prose or section titles rather than commit messages. Commit bodies were kept,
  minus the pasted terminal output some of them carried.
- **The internal working directories were removed from every commit.** They held the
  project's audit rounds and its day-to-day working notes: never part of the library, never
  shipped in a package, and not material anyone outside the project has a use for. The public
  summary of the audit work is `docs/05-quality/audit-record.md`, and it is the record that
  matters.
- **Development-workflow files and machine-specific notes were removed.** Per-directory
  contributor guides, editor and tool state directories, and the sections of
  `docs/03-development/ci-architecture.md` that documented the owner's own hardware by name.
  None of it describes the library.

## What this means if you are reading the repository

**Commit SHAs cited in any document written before 2026-08-31 do not resolve.** Rewriting any
commit changes every descendant, so the entire graph is renumbered. References inside `docs/` and
`CHANGELOG.md` were re-pointed to the new SHAs as part of the same pass; a stale-looking short
hash in an older document is a reference the pass did not reach, not a missing commit.

Nothing was removed from the library, the tests, the benchmarks or the packaging. The tree at the
first public commit is the tree that was there before the rewrite, minus those two directories.

## Why before rather than after

A rewrite recalls nothing that has already been cloned. Once a repository is public, its history
is public permanently, whatever is force-pushed over it afterwards — so there is exactly one
moment at which this can be done, and it is before the first person clones it. Doing it in one
pass rather than two also meant one renumbering of the graph instead of two.

For the same reason the rewrite was published into a newly created repository rather than
force-pushed over the existing one. A force-push does not remove anything from a git host: pull
request refs survive it untouched, and unreferenced objects stay addressable by SHA. The
pre-rewrite repository is retained privately by the owner as the recovery copy, and is not
published.
