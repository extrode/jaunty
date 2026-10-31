#!/usr/bin/env bash
# Tests for scripts/check-authorship.sh. Runs anywhere with bash and git, against a throwaway
# repository under tmp/.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
script="$repo/scripts/check-authorship.sh"
mkdir -p "$repo/tmp"
scr="$(mktemp -d "$repo/tmp/check-authorship-tests-XXXXXX")"
trap 'rm -rf -- "$scr"' EXIT

pass=0; fail=0
ok()   { echo "PASS  $1"; pass=$((pass + 1)); }
bad()  { echo "FAIL  $1"; [ -n "${2:-}" ] && printf '      %s\n' "$2"; fail=$((fail + 1)); }

commit() {
  local name="$1" email="$2" msg="$3"
  echo "$msg" >> "$scr/repo/file"
  git -C "$scr/repo" add file
  GIT_AUTHOR_NAME="$name" GIT_AUTHOR_EMAIL="$email" GIT_COMMITTER_NAME="$name" GIT_COMMITTER_EMAIL="$email" \
    git -C "$scr/repo" -c commit.gpgsign=false -c core.hooksPath=/dev/null commit -q -m "$msg"
  git -C "$scr/repo" rev-parse HEAD
}

run() { (cd "$scr/repo" && "$script" "$1") > "$scr/out" 2>&1; echo $?; }

git init -q -b scratch "$scr/repo"
base="$(commit 'Ada Lovelace' 'ada@example.com' 'base')"
human="$(commit 'Ada Lovelace' 'ada@example.com' 'human change')"

code="$(run "$base..$human")"
if [ "$code" = 0 ]; then ok "a human-only range passes"; else bad "a human-only range passes" "exit $code: $(cat "$scr/out")"; fi

code="$(run "1111111111111111111111111111111111111111..$human")"
if [ "$code" != 0 ] && grep -q "cannot resolve commit range" "$scr/out"; then
  ok "an unresolvable range fails with a message"
else
  bad "an unresolvable range fails with a message" "exit $code: $(cat "$scr/out")"
fi

code="$(run "no-such-ref..HEAD")"
if [ "$code" != 0 ]; then ok "an unknown ref fails"; else bad "an unknown ref fails" "exit $code: $(cat "$scr/out")"; fi

ai="$(commit 'Copilot' 'bot@example.com' 'tool change')"
code="$(run "$human..$ai")"
if [ "$code" = 1 ] && grep -q "AI author or committer" "$scr/out"; then
  ok "an AI author fails"
else
  bad "an AI author fails" "exit $code: $(cat "$scr/out")"
fi

trailer="$(commit 'Ada Lovelace' 'ada@example.com' "$(printf 'change\n\nCo-authored-by: ChatGPT <x@example.com>')")"
code="$(run "$ai..$trailer")"
if [ "$code" = 1 ] && grep -q "attribution trailer" "$scr/out"; then
  ok "an attribution trailer fails"
else
  bad "an attribution trailer fails" "exit $code: $(cat "$scr/out")"
fi

echo "$pass passed, $fail failed"
[ "$fail" = 0 ]
