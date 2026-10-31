#!/usr/bin/env bash
# Cleanup after rewriting the 9 dependabot[bot]-authored commits on main and dev (2026-10-31).
# Run only after `git push --force-with-lease ... origin main dev` has succeeded.
# Dry run by default. --execute deletes the merged branches (git branch -d refuses unmerged ones).
# --drop-backup-refs also deletes refs/original/* (the pre-rewrite history, via filter-branch).
# --delete-backup-bundle also deletes tmp/pre-dependabot-rewrite.bundle (the other copy).
set -euo pipefail

execute=0 drop_refs=0 del_bundle=0
for a in "$@"; do
  case "$a" in
    -e|--execute) execute=1 ;;
    --drop-backup-refs) drop_refs=1 ;;
    --delete-backup-bundle) del_bundle=1 ;;
    *) echo "unknown flag: $a" >&2; exit 2 ;;
  esac
done

repo="$(git rev-parse --show-toplevel)"
cd "$repo"
failed=0

if [ "$(git branch --show-current)" != "dev" ]; then
  echo "refuse: run from the dev checkout"; [ "$execute" = 1 ] && exit 1 || exit 0
fi
git fetch -q origin
for b in main dev; do
  if [ "$(git rev-parse "$b")" != "$(git rev-parse "origin/$b")" ]; then
    echo "refuse: $b differs from origin/$b; push the rewrite first"; [ "$execute" = 1 ] && exit 1 || exit 0
  fi
done

run() { if [ "$execute" = 1 ]; then "$@" || { echo "  refused: left alone"; failed=1; }; else echo "would: $*"; fi; }

echo "== 1. branches"
for b in chore/deps-repodb chore/cleanup-dependabot-rewrite; do
  if git rev-parse --verify --quiet "refs/heads/$b" >/dev/null; then run git branch -d "$b"; else echo "skip: $b gone"; fi
done

echo "== 2. filter-branch backup refs (needs --drop-backup-refs)"
for r in $(git for-each-ref refs/original --format='%(refname)'); do
  if [ "$drop_refs" = 1 ]; then run git update-ref -d "$r"; else echo "kept: $r (pass --drop-backup-refs)"; fi
done

echo "== 3. backup bundle (needs --delete-backup-bundle)"
p=tmp/pre-dependabot-rewrite.bundle
if [ ! -e "$p" ]; then echo "skip: $p absent"
elif git ls-files --error-unmatch -- "$p" >/dev/null 2>&1; then echo "refuse: $p is tracked by git. Left alone."; failed=1
elif [ "$del_bundle" = 1 ] && [ "$execute" = 1 ]; then rm -f "$p"; echo "deleted: $p"
else echo "kept: $p (pass -e --delete-backup-bundle)"; fi

echo "== state"
git branch --list; git for-each-ref refs/original --format='%(refname)'
[ "$execute" = 1 ] && [ "$failed" = 1 ] && exit 1
exit 0
