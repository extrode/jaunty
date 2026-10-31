#!/usr/bin/env bash
# Cleanup after audit round 39, the history rewrite and the GitHub hygiene pass (2026-10-31).
# Dry run by default. --execute acts on worktrees and temp branches (reversible).
# --drop-backup-refs also deletes refs/backup/* and refs/original/* (the pre-rewrite history).
# --delete-scratch also deletes untracked tmp/ scratch and the /tmp leftovers.
set -euo pipefail

execute=0 drop_refs=0 del_scratch=0
for a in "$@"; do
  case "$a" in
    -e|--execute) execute=1 ;;
    --drop-backup-refs) drop_refs=1 ;;
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

echo "== 1. worktrees"
for wt in chore-hygiene fix-audit-r39 worker-audit-core worker-audit-parser worker-rv-fix; do
  d="$repo/.worktrees/$wt"
  if git worktree list --porcelain | grep -qxF "worktree $d"; then run git worktree remove "$d"; else echo "skip: $wt not registered"; fi
done
[ "$execute" = 1 ] && git worktree prune

echo "== 2. branches (git branch -d refuses unmerged ones)"
for b in tmp-rw tmp-main fix/audit-r39 chore/worker-audit-core chore/worker-audit-parser chore/worker-rv-fix chore/github-hygiene fix/review-2026-10-31-1 chore/dependabot-pkcs-ignore; do
  if git rev-parse --verify --quiet "refs/heads/$b" >/dev/null; then run git branch -d "$b"; else echo "skip: $b gone"; fi
done

echo "== 3. pre-rewrite backup refs (needs --drop-backup-refs)"
for r in $(git for-each-ref refs/backup refs/original --format='%(refname)'); do
  if [ "$drop_refs" = 1 ]; then run git update-ref -d "$r"; else echo "kept: $r (pass --drop-backup-refs)"; fi
done

echo "== 4. untracked scratch (needs --delete-scratch)"
for p in tmp /tmp/wac_* /tmp/aotscan-47315; do
  [ -e "$p" ] || { echo "skip: $p absent"; continue; }
  if git ls-files --error-unmatch -- "$p" >/dev/null 2>&1; then
    echo "refuse: $p is tracked by git. Left alone."; failed=1; continue
  fi
  if [ "$p" = tmp ]; then
    if [ "$del_scratch" = 1 ] && [ "$execute" = 1 ]; then find tmp -mindepth 1 -maxdepth 1 -exec rm -rf {} +; else echo "kept: contents of tmp/"; fi
  elif [ "$del_scratch" = 1 ] && [ "$execute" = 1 ]; then rm -rf "$p"; else echo "kept: $p"; fi
done

echo "== state"
git worktree list; git branch --list; git for-each-ref refs/backup refs/original --format='%(refname)'
[ "$execute" = 1 ] && [ "$failed" = 1 ] && exit 1
exit 0
