# Self-hosted CI runner

Why this exists, how it is wired, and what to do when it breaks.

## Why

From 2026-07-29 GitHub stopped starting jobs on hosted runners:

```
The job was not started because recent account payments have failed
or your spending limit needs to be increased.
```

Actions itself is still enabled on the repo (`gh api repos/extrode/jaunty/actions/permissions`
returns `"enabled": true`) — only the **hosted** minutes are blocked. GitHub does not bill minutes
for self-hosted runners, so a runner on the dev machine sidesteps the block without touching a
single workflow step.

## How it is wired

`runs-on` is not hardcoded. Every job reads a repository variable:

```yaml
runs-on: ${{ vars.CI_RUNNER || 'ubuntu-latest' }}
```

| `CI_RUNNER` | Effect |
|---|---|
| `self-hosted` | jobs run on the WSL2 runner (current setting) |
| unset / deleted | jobs fall back to `ubuntu-latest` — no workflow edit needed |

So switching back once billing is fixed is one command:

```bash
gh variable delete CI_RUNNER --repo extrode/jaunty
```

The runner itself:

| Property | Value |
|---|---|
| Name | `REDACTED-RUNNER` |
| Host | WSL2 Debian on the Windows dev box |
| Path | `~/actions-runner` inside the Debian distro |
| Labels | `self-hosted`, `Linux`, `X64`, `jaunty-wsl` |

## Setup

Registration is done. Two steps remain, both needing a password this tooling cannot supply.

### 1. Enable Docker Desktop's WSL integration for Debian

**Required.** The `build-and-test` job declares a SQL Server 2022 **service container**, which the
runner starts with Docker. Debian currently resolves `docker` to the Windows binary under
`/mnt/c/Program Files/...` and has no `/var/run/docker.sock`, so service containers cannot start.

Docker Desktop → Settings → Resources → WSL Integration → enable **Debian** → Apply & Restart.

Verify:

```bash
wsl -d Debian -- docker version --format '{{.Server.Version}}'
```

### 2. Install and start the runner service

```bash
wsl -d Debian
cd ~/actions-runner
sudo ./svc.sh install
sudo ./svc.sh start
./svc.sh status
```

systemd is enabled in this distro (`/etc/wsl.conf` has `systemd=true`), so the service survives a
distro restart. Confirm GitHub sees it:

```bash
gh api repos/extrode/jaunty/actions/runners --jq '.runners[] | "\(.name) \(.status)"'
```

`online` means the next push runs here.

## What you give up

- **CI only runs while this machine is on** and the runner service is up. Jobs otherwise sit
  queued, and GitHub cancels a queued job after 24 hours. Hosted runners had no such dependency.
- **Never do this on a public repo.** A self-hosted runner on a public repo executes arbitrary code
  from any fork's pull request, on the dev machine. This matters because "public repo + branch
  protection" is a live milestone — if the repo goes public, move the runner to a disposable VM or
  go back to hosted.
- The runner reuses one working directory across runs. It is not the clean VM a hosted runner gives
  you, so a job that leaves state behind can influence the next one. `actions/checkout` cleans the
  tree, but not Docker volumes or anything written outside the workspace.
- **CI competes with the local test suite for memory.** A CI run adds a second SQL Server container
  on top of the four `torture-*` databases. On 2026-07-29 that combination OOM-killed all four
  (`docker ps -a` showed `Exited (137)`) in the middle of a local `dotnet test`, which then reported
  hundreds of MariaDB and Postgres failures that had nothing to do with the code. If a local run
  fails wholesale against one provider, check `docker ps` before believing it.

## Runner-specific workflow changes

Two things in the workflows exist only because of the self-hosted runner. Both are harmless on
hosted runners, so there is nothing to undo when billing is restored.

- `runs-on: ${{ vars.CI_RUNNER || 'ubuntu-latest' }}` — see above.
- `DOTNET_INSTALL_DIR: ${{ runner.tool_cache }}/dotnet` on every `actions/setup-dotnet` step.
  The action installs into `/usr/share/dotnet` by default. On this runner that path already holds
  Debian's root-owned .NET 10 SDK, so the unprivileged runner user cannot write to it and the step
  fails with a wall of `mkdir: Permission denied`. `runner.tool_cache` is writable on hosted and
  self-hosted alike.
- The `mssql` service container publishes **1435**, not 1433. A hosted runner gets a private VM, so
  1433 was free; this runner shares a Docker engine and a localhost with `torture-mssql`, which
  already owns 1433. With both on 1433 whichever starts second fails to bind, and Docker leaves the
  loser running with no host mapping at all — the container looks healthy while nothing can reach
  it. `JAUNTY_TEST_SQLSERVER` in the workflow points at `localhost,1435` to match.

## Troubleshooting

**Runner shows `offline`.** The service is not running. `wsl -d Debian -- bash -lc 'cd
~/actions-runner && ./svc.sh status'`. WSL shuts distros down when idle; the systemd service starts
with the distro, but the distro still has to be running.

**`build-and-test` fails to start the service container.** Docker Desktop WSL integration is off —
see step 1.

**Jobs queue forever.** Either no runner matches the labels, or `CI_RUNNER` names something that
does not exist. `gh variable list --repo extrode/jaunty` and compare against the runner's labels.

**A job passes here and would fail on hosted.** The runner is Debian with the .NET 10 SDK already
installed; hosted was `ubuntu-latest`. `actions/setup-dotnet` still pins 8.0.x and 9.0.x per the
workflow, but anything relying on the ambient image differs. Treat a green self-hosted run as
weaker evidence than a green hosted run until billing is restored.

## Related
- `.github/workflows/ci.yml`, `.github/workflows/release.yml`.
