#!/usr/bin/env bash
# Written 2026-09-24 by Claude. Owner ruling that day: on mb1, ~/Developer holds code under
# ~/Developer/code/<domain>/<repo>, same as the Windows box, and nothing else. The mutation-runner
# setup of 2026-09-24 added four top-level folders that do not fit that layout. This script moves
# what is worth keeping into the repos' gitignored tmp/ and removes the rest. Run ON mb1.
#
#   ~/Developer/bin/mutate             -> ~/Developer/code/extrode.com/jaunty/tmp/mb1-mutate/mutate
#   ~/Developer/mutation-reports/<r>/  -> ~/Developer/code/extrode.com/<r>/tmp/mutation-reports/
#   ~/Developer/bundles/               removed (git bundles already fetched into the clones)
#   ~/Developer/tools/stryker-4.16.0/  removed (side-by-side comparison with Stryker 5 is done)
#   ~/Developer/bin, ~/Developer/tools removed only if empty afterwards
#
# After this, run the runner as:
#   MUTATE_REPORTS=~/Developer/code/extrode.com/jaunty/tmp/mutation-reports \
#     ~/Developer/code/extrode.com/jaunty/tmp/mb1-mutate/mutate jaunty --ref dev
#
# Dry run (default):  bash mb1-developer-extras.sh
# Apply:              bash mb1-developer-extras.sh --execute --delete-tools
set -euo pipefail

DEV="$HOME/Developer"
CODE="$DEV/code/extrode.com"
JAUNTY="$CODE/jaunty"

EXECUTE=0; DELETE=0
for arg in "$@"; do
    case "$arg" in
        --execute|-e) EXECUTE=1 ;;
        --delete-tools) DELETE=1 ;;
        *) echo "unknown argument: $arg"; exit 2 ;;
    esac
done

if [ "$EXECUTE" -eq 1 ] && [ "$DELETE" -eq 0 ]; then
    echo "refuse: removing the bundles and the Stryker 4.16 install is irreversible; pass --delete-tools with --execute."
    exit 2
fi

[ "$EXECUTE" -eq 1 ] && echo "== apply ==" || echo "== dry run (pass --execute --delete-tools to apply) =="

# 1. Gate: must be mb1, the Jaunty clone must exist, and no mutation run may be in progress.
if [ "$(scutil --get LocalHostName 2>/dev/null || hostname -s)" != "mb1" ]; then
    echo "refuse: this is not mb1."; exit 1
fi
if [ ! -d "$JAUNTY/.git" ]; then
    echo "refuse: $JAUNTY is missing."; exit 1
fi
if pgrep -f "dotnet-stryker|Stryker.CLI|/bin/mutate" >/dev/null 2>&1; then
    echo "refuse: a mutation run is in progress. Wait for it to finish."; exit 1
fi

failed=0

# Destination must be ignored by the repo, so nothing moved in shows up as a change.
ignored() {
    local repo="$1" rel="$2"
    git -C "$repo" check-ignore -q -- "$rel"
}

move() {
    local src="$1" dest="$2"
    if [ -e "$dest" ]; then
        echo "refuse: $dest already exists. Left $src alone."; failed=1; return
    fi
    if [ "$EXECUTE" -eq 1 ]; then
        echo "-- move $src -> $dest"
        mkdir -p "$(dirname "$dest")"
        mv -- "$src" "$dest"
    else
        echo "would move $src -> $dest"
    fi
}

remove() {
    local dir="$1"
    local size; size="$(du -sh "$dir" | cut -f1)"
    if [ "$EXECUTE" -eq 1 ]; then
        echo "-- remove $dir ($size)"
        rm -rf -- "$dir"
    else
        echo "would remove $dir ($size)"
    fi
}

# 2. The runner script.
if [ ! -e "$DEV/bin/mutate" ]; then
    echo "skip:  $DEV/bin/mutate is already gone"
elif ! ignored "$JAUNTY" "tmp/mb1-mutate/mutate"; then
    echo "refuse: jaunty's tmp/ is not gitignored. Left $DEV/bin/mutate alone."; failed=1
else
    move "$DEV/bin/mutate" "$JAUNTY/tmp/mb1-mutate/mutate"
fi

# 3. Mutation reports, one folder per repo.
if [ ! -d "$DEV/mutation-reports" ]; then
    echo "skip:  $DEV/mutation-reports is already gone"
else
    for src in "$DEV/mutation-reports"/*; do
        [ -e "$src" ] || continue
        repo="$(basename "$src")"
        if [ ! -d "$CODE/$repo/.git" ]; then
            echo "refuse: no clone $CODE/$repo for $src. Left alone."; failed=1; continue
        fi
        if ! ignored "$CODE/$repo" "tmp/mutation-reports"; then
            echo "refuse: $repo's tmp/ is not gitignored. Left $src alone."; failed=1; continue
        fi
        move "$src" "$CODE/$repo/tmp/mutation-reports"
    done
    if [ "$EXECUTE" -eq 1 ]; then
        rmdir "$DEV/mutation-reports" 2>/dev/null && echo "-- remove empty $DEV/mutation-reports" || true
    fi
fi

# 4. Bundles: remove only if the folder holds nothing but *.bundle files.
if [ ! -d "$DEV/bundles" ]; then
    echo "skip:  $DEV/bundles is already gone"
elif find "$DEV/bundles" -mindepth 1 ! -name "*.bundle" | grep -q .; then
    echo "refuse: $DEV/bundles holds something other than git bundles. Left alone."; failed=1
else
    remove "$DEV/bundles"
fi

# 5. Stryker 4.16 side-by-side install.
if [ ! -d "$DEV/tools/stryker-4.16.0" ]; then
    echo "skip:  $DEV/tools/stryker-4.16.0 is already gone"
else
    remove "$DEV/tools/stryker-4.16.0"
fi

# 6. Parent folders, only when empty.
for dir in "$DEV/bin" "$DEV/tools"; do
    if [ ! -d "$dir" ]; then
        echo "skip:  $dir is already gone"
    elif [ -n "$(ls -A "$dir")" ]; then
        if [ "$EXECUTE" -eq 1 ]; then
            echo "hold:  $dir still holds: $(ls -A "$dir" | tr '\n' ' ')"
        else
            echo "would remove $dir if empty after the steps above (holds: $(ls -A "$dir" | tr '\n' ' '))"
        fi
    elif [ "$EXECUTE" -eq 1 ]; then
        rmdir "$dir" && echo "-- remove empty $dir"
    else
        echo "would remove empty $dir"
    fi
done

# 7. What is left.
echo "== ~/Developer now holds =="
ls -A "$DEV"

if [ "$EXECUTE" -eq 1 ] && [ "$failed" -eq 1 ]; then exit 1; fi
