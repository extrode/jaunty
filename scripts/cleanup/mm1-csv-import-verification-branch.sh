#!/usr/bin/env bash
# Written 2026-09-16 by Claude after verifying the CsvImport.cs macOS symlink fix
# (fix/csv-import-symlink-realpath-macos) on mm1 via a git-bundle transfer. Removes the
# throwaway verification branch and bundle file left behind on mm1 once the fix is
# safely merged into dev on the primary Windows checkout. Run ON mm1, not on Windows.
#
# Dry run (default):  bash mm1-csv-import-verification-branch.sh
# Apply:               bash mm1-csv-import-verification-branch.sh --execute
set -eu

REPO="$HOME/Developer/code/extrode.com/jaunty"
BRANCH="fix/csv-import-symlink-realpath-macos"
BUNDLE="$HOME/jaunty-fix.bundle"

EXECUTE=0
for arg in "$@"; do
    case "$arg" in
        --execute|-e) EXECUTE=1 ;;
    esac
done

failed=0

if [ "$EXECUTE" -eq 1 ]; then
    echo "== apply =="
else
    echo "== dry run (pass --execute to apply) =="
fi

if [ ! -d "$REPO/.git" ]; then
    echo "refuse: $REPO is not a git repo. Nothing to do."
    exit 1
fi

cd "$REPO"

current_branch="$(git rev-parse --abbrev-ref HEAD)"
if [ "$current_branch" = "$BRANCH" ]; then
    if [ "$EXECUTE" -eq 1 ]; then
        echo "-- git checkout dev"
        git checkout dev
    else
        echo "would run: git checkout dev (currently on $BRANCH)"
    fi
else
    echo "skip: already on '$current_branch', not '$BRANCH'"
fi

if ! git rev-parse --verify --quiet "refs/heads/$BRANCH" >/dev/null; then
    echo "skip: branch $BRANCH no longer exists"
else
    if [ "$EXECUTE" -eq 1 ]; then
        echo "-- git branch -d $BRANCH"
        if ! git branch -d "$BRANCH"; then
            echo "   refused: not merged into current branch. Left alone."
            failed=1
        fi
    else
        echo "would run: git branch -d $BRANCH (refuses automatically if unmerged)"
    fi
fi

if [ ! -e "$BUNDLE" ]; then
    echo "skip: $BUNDLE no longer exists"
elif git -C "$REPO" ls-files --error-unmatch -- "$BUNDLE" >/dev/null 2>&1; then
    echo "refuse: $BUNDLE is tracked by git. Propose it as a commit instead. Left alone."
    failed=1
else
    if [ "$EXECUTE" -eq 1 ]; then
        echo "-- rm $BUNDLE"
        rm -f "$BUNDLE"
    else
        echo "would run: rm -f $BUNDLE (untracked scratch file)"
    fi
fi

echo
echo "== remaining state =="
git -C "$REPO" status --porcelain --branch
git -C "$REPO" branch --list "$BRANCH" || true
ls -la "$BUNDLE" 2>/dev/null || echo "$BUNDLE: absent"

if [ "$EXECUTE" -eq 1 ] && [ "$failed" -eq 1 ]; then
    exit 1
fi
