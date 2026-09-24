#!/usr/bin/env bash
# Tests for scripts/mutation/mutate.sh. Runs anywhere with bash, git, python3 + PyYAML and
# column; nothing here starts Stryker. caffeinate is replaced by a stub that records its
# arguments, so the launcher is exercised up to the point it would hand off to the detached run.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
runner="$repo/scripts/mutation/mutate.sh"
mkdir -p "$repo/tmp"
scr="$(mktemp -d "$repo/tmp/mutate-tests-XXXXXX")"
trap 'rm -rf -- "$scr"' EXIT

pass=0; fail=0
ok()   { echo "PASS  $1"; pass=$((pass + 1)); }
bad()  { echo "FAIL  $1"; [ -n "${2:-}" ] && printf '      %s\n' "$2"; fail=$((fail + 1)); }
check() { if eval "$2"; then ok "$1"; else bad "$1" "${3:-}"; fi; }

root="$scr/code"
mkdir -p "$root" "$scr/bin" "$scr/home"
cat > "$scr/bin/caffeinate" <<EOF
#!/usr/bin/env bash
shift
printf '%s\n' "\$@" > "$scr/caffeinate.args"
EOF
chmod +x "$scr/bin/caffeinate"

run() { HOME="$scr/home" PATH="$scr/bin:$PATH" MUTATE_ROOT="$root" "$@"; }

fixture() {
  local name="$1" body="$2"
  mkdir -p "$root/$name/.github/workflows"
  printf '%s\n' "$body" > "$root/$name/.github/workflows/nightly.yml"
}

fixture jshape 'jobs:
  test:
    steps:
      - run: dotnet test
  mutation:
    strategy:
      matrix:
        project: [A.Tests, B.Tests]
        include:
          - project: B.Tests
            extra-test-project: --test-project ../C/C.csproj
    steps:
      - run: dotnet stryker --concurrency 16'

fixture qshape 'jobs:
  mutation:
    strategy:
      matrix:
        include:
          - label: core
            directory: Q.Core.Tests
            configFile: stryker-core.json
            extraArgs: --since
    steps:
      - run: dotnet stryker'

fixture nostryker 'jobs:
  test:
    steps:
      - run: dotnet test'

out="$(run "$runner" jshape --list 2>&1)"; code=$?
check "list: jaunty-shape matrix exits 0" '[ $code -eq 0 ]' "$out"
check "list: include merges extra-test-project onto its own row only" \
  'echo "$out" | grep -q "^B.Tests.*--test-project ../C/C.csproj" && ! echo "$out" | grep -q "^A.Tests.*--test-project"' "$out"

out="$(run "$runner" qshape --list 2>&1)"
check "list: jauntyq-shape include row keeps label, directory, config and args" \
  'echo "$out" | grep -qE "^core +Q.Core.Tests +stryker-core.json +--since"' "$out"

out="$(run "$runner" nostryker --list 2>&1)"; code=$?
check "list: workflow with no stryker job fails" '[ $code -ne 0 ] && echo "$out" | grep -q "no job in nightly.yml runs stryker"' "$out"

out="$(run "$runner" 2>&1)"; code=$?
check "usage: missing repo exits 2" '[ $code -eq 2 ] && echo "$out" | grep -q "usage: mutate.sh <repo>"' "$out"

out="$(run "$runner" jshape --bogus 2>&1)"; code=$?
check "usage: unknown flag exits 2" '[ $code -eq 2 ] && echo "$out" | grep -q "unknown flag: --bogus"' "$out"

stamp() {
  mkdir -p "$1"
  printf '%s\n' "$2" > "$1/summary.md"
  touch -d "$3" "$1"
}
stamp "$root/jshape/tmp/mutation-reports/20260101-0000-abc1234" "# jshape old" "2026-01-01 00:00"
stamp "$root/jshape/tmp/mutation-reports/20260102-0000-def5678" "# jshape new" "2026-01-02 00:00"
stamp "$root/qshape/tmp/mutation-reports/20260103-0000-0a1b2c3" "# qshape newest" "2026-01-03 00:00"
mkdir -p "$root/jshape/tmp/mutation-reports/not-a-run"

out="$(run "$runner" status jshape 2>&1)"
check "status <repo>: reads <repo>/tmp/mutation-reports and picks the newest run" \
  'echo "$out" | grep -q "# jshape new" && ! echo "$out" | grep -q "# jshape old"' "$out"

out="$(run "$runner" status 2>&1)"
check "status: without a repo picks the newest run across repos" 'echo "$out" | grep -q "# qshape newest"' "$out"

running="$root/qshape/tmp/mutation-reports/20260105-0000-2222222"
mkdir -p "$running" && printf '== core  (tests/Q.Core.Tests)\n' > "$running/run.log" && touch -d "2026-01-05 00:00" "$running"
out="$(run "$runner" status qshape 2>&1)"; code=$?
check "status: run in progress (log, no summary yet) shows the log and exits 0" \
  '[ $code -eq 0 ] && echo "$out" | grep -q "== core  (tests/Q.Core.Tests)"' "$out"

stamp "$scr/reports/jshape/20260104-0000-1111111" "# override" "2026-01-04 00:00"
out="$(MUTATE_REPORTS="$scr/reports" run "$runner" status jshape 2>&1)"
check "status: MUTATE_REPORTS overrides the report location" \
  'echo "$out" | grep -q "# override" && ! echo "$out" | grep -q "# jshape new"' "$out"

g() { git -c core.hooksPath=/dev/null -c core.autocrlf=false -c user.name=t -c user.email=t@t -c init.defaultBranch=main "$@"; }
src="$scr/src"
g init -q "$src"
mkdir -p "$src/.github/workflows" "$src/scripts/mutation"
cp "$root/jshape/.github/workflows/nightly.yml" "$src/.github/workflows/nightly.yml"
cp "$runner" "$src/scripts/mutation/mutate.sh"
g -C "$src" add -A && g -C "$src" commit -qm "chore: with runner"
g -C "$src" checkout -qb dev
g -C "$src" rm -q scripts/mutation/mutate.sh && g -C "$src" commit -qm "chore: without runner"
g -C "$src" bundle create -q "$scr/dev.bundle" dev 2>/dev/null
g clone -q --branch main "$src" "$root/fx"
printf 'tmp/\n' > "$root/fx/.git/info/exclude"
cp "$root/fx/scripts/mutation/mutate.sh" "$scr/launched.sh"

out="$(run "$root/fx/scripts/mutation/mutate.sh" fx --bundle "$scr/dev.bundle" --ref dev --foreground 2>&1)"; code=$?
copy="$(ls "$root"/fx/tmp/mutation-reports/*/mutate.sh 2>/dev/null | head -1)"
check "launch: ref without the runner removes it from the checkout" '[ ! -e "$root/fx/scripts/mutation/mutate.sh" ]'
check "launch: exits 0 after the checkout removed the running script" '[ $code -eq 0 ]' "$out"
check "launch: report folder holds a byte-identical copy of the runner" '[ -n "$copy" ] && cmp -s "$copy" "$scr/launched.sh"' "$copy"
check "launch: detached run is started from the copy" \
  '[ -f "$scr/caffeinate.args" ] && [ "$(sed -n 1p "$scr/caffeinate.args")" = "$copy" ] && sed -n 2,3p "$scr/caffeinate.args" | tr "\n" " " | grep -q "^__run fx "' \
  "$(cat "$scr/caffeinate.args" 2>/dev/null)"

echo
echo "$pass passed, $fail failed"
[ "$fail" -eq 0 ]
