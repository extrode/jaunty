#!/usr/bin/env bash
# Fails when any commit in <range> is authored or committed by an AI tool, or carries an AI
# attribution trailer. Contributions must be authored by a human (CLA.md Section 6).
# Usage: scripts/check-authorship.sh <rev-range>   e.g. origin/dev..HEAD
set -euo pipefail
range="${1:?usage: check-authorship.sh <rev-range>}"
pattern='claude|anthropic|copilot|chatgpt|openai|gemini'
bad=0
while IFS=$'\t' read -r sha an ae cn ce; do
  if printf '%s %s %s %s' "$an" "$ae" "$cn" "$ce" | grep -Eiq "$pattern"; then
    echo "::error::$sha has an AI author or committer: $an <$ae> / $cn <$ce>"; bad=1
  fi
done < <(git log --format='%H%x09%an%x09%ae%x09%cn%x09%ce' "$range")
for sha in $(git rev-list "$range"); do
  if git log -1 --format=%B "$sha" | grep -Eiq "^(co-authored-by:.*($pattern)|claude-session:|.*generated with .*(claude|copilot|chatgpt))"; then
    echo "::error::$sha carries an AI attribution trailer"; bad=1
  fi
done
exit $bad
