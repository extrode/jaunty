#!/usr/bin/env bash
# Written 2026-09-24 by Claude. Owner ruling that day: on mb1, Jaunty lives only in
# ~/Developer/code/extrode.com/jaunty. Removes the four other Jaunty clones found there
# (7.9 GB): the pre-org-move beparey.com clone and three ~/jaunty-bench* copies made from
# git bundles on 2026-09-19. Run ON mb1.
#
# Each clone is removed only if, at run time: its working tree is clean, it has no commit
# missing from its own remote, nothing has a file open inside it, and its tip commit is
# reachable from the kept clone or from the remote it was cloned from. Clones whose tip
# predates the rc.2 history rewrite (beparey.com/jaunty, jaunty-bench-pre-rewrite) are not in
# the kept clone; their tips live in the Windows box's jaunty-pre-rewrite repo, checked
# 2026-09-24. Those need --include-pre-rewrite as well.
#
# Dry run (default):  bash mb1-jaunty-single-location.sh
# Apply:              bash mb1-jaunty-single-location.sh --execute --delete-clones [--include-pre-rewrite]
set -euo pipefail

KEEP="$HOME/Developer/code/extrode.com/jaunty"
TARGETS=(
    "$HOME/jaunty-bench"
    "$HOME/jaunty-bench-pre-scope-fix-2026-09-19"
    "$HOME/jaunty-bench-pre-rewrite-2026-09-19"
    "$HOME/Developer/code/beparey.com/jaunty"
)

EXECUTE=0; DELETE=0; PRE_REWRITE=0
for arg in "$@"; do
    case "$arg" in
        --execute|-e) EXECUTE=1 ;;
        --delete-clones) DELETE=1 ;;
        --include-pre-rewrite) PRE_REWRITE=1 ;;
        *) echo "unknown argument: $arg"; exit 2 ;;
    esac
done

if [ "$EXECUTE" -eq 1 ] && [ "$DELETE" -eq 0 ]; then
    echo "refuse: removing a clone is irreversible; pass --delete-clones with --execute."
    exit 2
fi

[ "$EXECUTE" -eq 1 ] && echo "== apply ==" || echo "== dry run (pass --execute --delete-clones to apply) =="

# 1. Gate: must be mb1, and the kept clone must exist.
if [ "$(scutil --get LocalHostName 2>/dev/null || hostname -s)" != "mb1" ]; then
    echo "refuse: this is not mb1."; exit 1
fi
if [ ! -d "$KEEP/.git" ]; then
    echo "refuse: the kept clone $KEEP is missing."; exit 1
fi

failed=0

# 2. Each target.
for dir in "${TARGETS[@]}"; do
    if [ ! -e "$dir" ]; then
        echo "skip:  $dir is already gone"
        continue
    fi
    if [ ! -d "$dir/.git" ]; then
        echo "refuse: $dir is not a git clone. Left alone."; failed=1; continue
    fi

    dirty="$(git -C "$dir" status --porcelain | wc -l | tr -d ' ')"
    if [ "$dirty" != "0" ]; then
        echo "refuse: $dir has $dirty uncommitted change(s). Left alone."; failed=1; continue
    fi

    local_only="$(git -C "$dir" log --branches --not --remotes --oneline | wc -l | tr -d ' ')"
    if [ "$local_only" != "0" ]; then
        echo "refuse: $dir has $local_only commit(s) on no remote. Left alone."; failed=1; continue
    fi

    if lsof +D "$dir" >/dev/null 2>&1; then
        echo "refuse: a process has a file open under $dir. Left alone."; failed=1; continue
    fi

    tip="$(git -C "$dir" rev-parse HEAD)"
    if git -C "$KEEP" cat-file -e "$tip^{commit}" 2>/dev/null; then
        where="tip ${tip:0:8} is in the kept clone"
    elif [ "$PRE_REWRITE" -eq 1 ]; then
        where="tip ${tip:0:8} predates the rewrite (--include-pre-rewrite)"
    else
        echo "hold:  $dir tip ${tip:0:8} is not in the kept clone; pass --include-pre-rewrite to remove it."
        continue
    fi

    size="$(du -sh "$dir" | cut -f1)"
    if [ "$EXECUTE" -eq 1 ]; then
        echo "-- remove $dir ($size, $where)"
        rm -rf -- "$dir"
    else
        echo "would remove $dir ($size, $where)"
    fi
done

# 3. What is left.
echo "== Jaunty clones remaining =="
find "$HOME" -maxdepth 5 -type d -name .git -path "*aunty*" -not -path "*/jauntyq*" 2>/dev/null | sed 's|/.git$||'

if [ "$EXECUTE" -eq 1 ] && [ "$failed" -eq 1 ]; then exit 1; fi
