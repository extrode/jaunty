#!/usr/bin/env bash
# Cleanup after audit round 40 (GetRequired struct fix, XML docs, review fix), 2026-10-31.
# Dry run by default. --execute deletes the merged branches (git branch -d refuses unmerged ones).
# --delete-scratch also deletes the untracked round-40 scratch under tmp/ and /tmp.
set -euo pipefail

execute=0 del_scratch=0
for a in "$@"; do
  case "$a" in
    -e|--execute) execute=1 ;;
    --delete-scratch) del_scratch=1 ;;
    *) echo "unknown flag: $a" >&2; exit 2 ;;
  esac
done

repo="$(git rev-parse --show-toplevel)"
cd "$repo"
failed=0

if [ "$(git branch --show-current)" != "dev" ]; then
  echo "refuse: run from the dev checkout"; [ "$execute" = 1 ] && exit 1 || exit 0
fi

run() { if [ "$execute" = 1 ]; then "$@" || { echo "  refused: left alone"; failed=1; }; else echo "would: $*"; fi; }

echo "== 1. branches"
for b in fix/audit-r40 fix/get-required-struct docs/xml-docs-r40 docs/changelog-r40 chore/deps-db-providers fix/review-2026-10-31-2 chore/cleanup-r40; do
  if git rev-parse --verify --quiet "refs/heads/$b" >/dev/null; then run git branch -d "$b"; else echo "skip: $b gone"; fi
done

echo "== 2. untracked scratch (needs --delete-scratch)"
for p in tmp/r40 tmp/r40audit /tmp/jaudit_40003; do
  [ -e "$p" ] || { echo "skip: $p absent"; continue; }
  case "$p" in /*) ;; *)
    if git ls-files --error-unmatch -- "$p" >/dev/null 2>&1; then
      echo "refuse: $p is tracked by git. Propose it as a commit instead. Left alone."; failed=1; continue
    fi ;;
  esac
  if [ "$del_scratch" = 1 ] && [ "$execute" = 1 ]; then rm -rf "$p"; else echo "kept: $p (pass -e --delete-scratch)"; fi
done

echo "== state"
git branch --list
[ "$execute" = 1 ] && [ "$failed" = 1 ] && exit 1
exit 0
