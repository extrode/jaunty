#!/usr/bin/env bash
# mutate - run a repo's Stryker.NET mutation tier on this Mac, the same jobs its CI would run.
#
# The job list is read from the repo's own .github/workflows/nightly.yml (the job whose steps
# call stryker), so it cannot drift from CI. Works for any repo under $MUTATE_ROOT whose matrix
# uses the jaunty shape (project + extra-test-project) or the jauntyq shape (label, directory,
# configFile, extraArgs).
#
#   mutate <repo> [--ref <branch>] [--bundle <file>] [--only a,b] [--concurrency N]
#                 [--list] [--foreground]
#   mutate status [<repo>]
#
#   <repo>         directory name under $MUTATE_ROOT, cloned from github.com/extrode/<repo> if absent
#   --ref          branch to test (default: dev)
#   --bundle       fetch <ref> from a git bundle instead of origin (for unpushed work)
#   --only         comma-separated job labels to run (default: all)
#   --concurrency  Stryker --concurrency (default: 8; this Mac has 10 cores and other users)
#   --list         print the jobs and exit
#   --foreground   run attached; default is detached under nohup + caffeinate, so an SSH
#                  disconnect or idle sleep does not kill a multi-hour run
#
# Reports: $MUTATE_REPORTS/<repo>/<stamp>-<sha>/<label>/reports/, summary.md alongside.
set -euo pipefail

MUTATE_ROOT="${MUTATE_ROOT:-$HOME/Developer/code/extrode.com}"
MUTATE_REPORTS="${MUTATE_REPORTS:-$HOME/Developer/mutation-reports}"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:/Applications/Docker.app/Contents/Resources/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

die() { echo "mutate: $*" >&2; exit 2; }

if [[ "${1:-}" == status ]]; then
  base="$MUTATE_REPORTS${2:+/$2}"
  pgrep -fl "dotnet-stryker|Stryker.CLI|mutate __run" || echo "no mutation run in progress"
  latest=$(ls -td "$base"/*/ "$base"/*/*/ 2>/dev/null | grep -E '/[0-9]{8}-[0-9]{4}-[0-9a-f]+/$' | head -1 || true)
  [[ -n "$latest" ]] && { echo "latest: $latest"; tail -5 "$latest/run.log" 2>/dev/null; [[ -f "$latest/summary.md" ]] && cat "$latest/summary.md"; }
  exit 0
fi

RUN_DETACHED=0
if [[ "${1:-}" == __run ]]; then RUN_DETACHED=1; shift; fi

REPO="${1:-}"; [[ -n "$REPO" && "$REPO" != -* ]] || die "usage: mutate <repo> [--ref b] [--bundle f] [--only a,b] [--concurrency N] [--list] [--foreground]"
shift
REF=dev BUNDLE="" ONLY="" CONC=8 LIST=0 FG=0 OUT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --ref) REF="$2"; shift 2 ;;
    --bundle) BUNDLE="$2"; shift 2 ;;
    --only) ONLY="$2"; shift 2 ;;
    --concurrency) CONC="$2"; shift 2 ;;
    --list) LIST=1; shift ;;
    --foreground) FG=1; shift ;;
    --out) OUT="$2"; shift 2 ;;
    *) die "unknown flag: $1" ;;
  esac
done

DIR="$MUTATE_ROOT/$REPO"

jobs_tsv() {
  python3 - "$DIR/.github/workflows/nightly.yml" <<'PY'
import itertools, sys, yaml
wf = yaml.safe_load(open(sys.argv[1]))
job = next((j for j in wf.get("jobs", {}).values()
            if any("stryker" in str(s.get("run", "")) for s in j.get("steps", []))), None)
if job is None:
    sys.exit("no job in nightly.yml runs stryker")
m = dict(job.get("strategy", {}).get("matrix", {}))
include = m.pop("include", []) or []
m.pop("exclude", None)
dims = {k: v for k, v in m.items() if isinstance(v, list)}
combos = [dict(zip(dims, vals)) for vals in itertools.product(*dims.values())] if dims else []
for inc in include:
    keys = [k for k in inc if k in dims]
    hits = [c for c in combos if keys and all(c[k] == inc[k] for k in keys)]
    for c in hits:
        c.update(inc)
    if not hits:
        combos.append(dict(inc))
known = {"label", "project", "directory", "configFile", "extraArgs", "extra-test-project"}
for c in combos:
    unknown = set(c) - known
    if unknown:
        print(f"warning: ignoring matrix keys {sorted(unknown)}", file=sys.stderr)
    label = c.get("label") or c["project"]
    directory = c.get("directory") or c["project"]
    extra = " ".join(x for x in (c.get("extraArgs", ""), c.get("extra-test-project", "")) if x)
    print("|".join([label, directory, c.get("configFile", "") or "", extra]))
PY
}

sync_repo() {
  if [[ ! -d "$DIR/.git" ]]; then
    git clone --quiet "https://github.com/extrode/$REPO.git" "$DIR"
  fi
  if [[ -n "$(git -C "$DIR" status --porcelain --untracked-files=no)" ]]; then
    die "$DIR has uncommitted changes to tracked files; refusing to check out over them"
  fi
  if [[ -n "$BUNDLE" ]]; then
    git -C "$DIR" fetch --quiet "$BUNDLE" "+$REF:refs/remotes/bundle/$REF"
    git -C "$DIR" checkout --quiet --detach "bundle/$REF"
  else
    git -C "$DIR" fetch --quiet origin
    git -C "$DIR" checkout --quiet --detach "origin/$REF"
  fi
}

if [[ $RUN_DETACHED -eq 0 ]]; then
  # --list must not move the checkout: a run in progress, or another session, may be using it.
  if [[ $LIST -eq 1 && -f "$DIR/.github/workflows/nightly.yml" ]]; then jobs_tsv | column -t -s '|'; exit 0; fi
  sync_repo
  [[ -f "$DIR/.github/workflows/nightly.yml" ]] || die "$REPO has no .github/workflows/nightly.yml"
  if [[ $LIST -eq 1 ]]; then jobs_tsv | column -t -s '|'; exit 0; fi
  SHA=$(git -C "$DIR" rev-parse --short HEAD)
  OUT="$MUTATE_REPORTS/$REPO/$(date +%Y%m%d-%H%M)-$SHA"
  mkdir -p "$OUT"
  args=(--ref "$REF" --concurrency "$CONC" --out "$OUT")
  [[ -n "$ONLY" ]] && args+=(--only "$ONLY")
  if [[ $FG -eq 1 ]]; then
    caffeinate -ims "$0" __run "$REPO" "${args[@]}" 2>&1 | tee "$OUT/run.log"
    exit "${PIPESTATUS[0]}"
  fi
  nohup caffeinate -ims "$0" __run "$REPO" "${args[@]}" > "$OUT/run.log" 2>&1 < /dev/null &
  echo "started (pid $!), $REPO @ $SHA"
  echo "log:     $OUT/run.log"
  echo "status:  mutate status $REPO"
  exit 0
fi

# ---- detached body: repo already at the target commit ----
cd "$DIR"
echo "== $REPO @ $(git rev-parse --short HEAD) ($(git log -1 --format=%s)), concurrency $CONC, $(date)"

if [[ -x tools/native/sqlite-interop-osx-arm64/build.sh && ! -f tools/native/sqlite-interop-osx-arm64/artifacts/SQLite.Interop.dll ]]; then
  echo "== building SQLite.Interop for osx-arm64 (one-time per clone)"
  DOTNET_ROLL_FORWARD=LatestMajor tools/native/sqlite-interop-osx-arm64/build.sh
fi

dotnet tool restore
# Stryker's own "retry with a nuget restore" does not recover a never-restored multi-TFM test
# project (Extrode.Jaunty.Tests: "project.assets.json not found"), so restore everything up front.
sln=$(ls *.slnx *.sln 2>/dev/null | head -1 || true)
if [[ -n "$sln" ]]; then echo "== dotnet restore $sln"; dotnet restore "$sln" -v q; fi
echo "stryker $(dotnet tool list --local | awk '$1 == "dotnet-stryker" {print $2}')"

failed=0
# '|', not a tab: tab is IFS whitespace, so read would merge the empty configFile field away.
while IFS='|' read -r label directory config extra; do
  if [[ -n "$ONLY" && ",$ONLY," != *",$label,"* ]]; then continue; fi
  echo
  echo "== $label  (tests/$directory${config:+, $config}${extra:+, $extra})  $(date +%H:%M:%S)"
  start=$SECONDS
  cmd=(dotnet stryker --concurrency "$CONC" --output "$OUT/$label")
  [[ -n "$config" ]] && cmd+=(--config-file "$config")
  # shellcheck disable=SC2206
  [[ -n "$extra" ]] && cmd+=($extra)
  if (cd "tests/$directory" && "${cmd[@]}") > "$OUT/$label.log" 2>&1; then
    echo "   ok in $(( (SECONDS - start) / 60 )) min"
  else
    echo "   FAILED (exit $?) in $(( (SECONDS - start) / 60 )) min, see $label.log"
    failed=1
  fi
done < <(jobs_tsv)

python3 - "$OUT" > "$OUT/summary.md" <<'PY'
import glob, json, os, sys
out = sys.argv[1]
rows = []
for report in sorted(glob.glob(os.path.join(out, "*", "reports", "mutation-report.json"))):
    label = report[len(out) + 1:].split(os.sep)[0]
    counts = {}
    for f in json.load(open(report))["files"].values():
        for m in f["mutants"]:
            counts[m["status"]] = counts.get(m["status"], 0) + 1
    det = counts.get("Killed", 0) + counts.get("Timeout", 0)
    und = counts.get("Survived", 0) + counts.get("NoCoverage", 0)
    score = 100 * det / (det + und) if det + und else float("nan")
    rows.append((label, score, counts))
print(f"# Mutation summary: {os.path.basename(out)}\n")
print("| Job | Score | Killed | Survived | NoCoverage | Timeout | CompileError | Ignored |")
print("|---|---|---|---|---|---|---|---|")
for label, score, c in rows:
    print(f"| {label} | {score:.2f}% | {c.get('Killed',0)} | {c.get('Survived',0)} | {c.get('NoCoverage',0)} "
          f"| {c.get('Timeout',0)} | {c.get('CompileError',0)} | {c.get('Ignored',0)} |")
PY
echo
cat "$OUT/summary.md"
echo "== finished $(date), failed=$failed"
exit $failed
