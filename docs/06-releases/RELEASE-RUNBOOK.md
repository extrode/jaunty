# Release Runbook

How to ship a Jaunty release, starting with `v1.0.0-rc.1`. The pipeline is
fully wired: pushing a version tag is the release action. Everything else
here is verification around that one step.

## How the pipeline works

`.github/workflows/release.yml` triggers on any tag matching `v*`:

1. Derives the package version from the tag (`v1.0.0-rc.1` -> `1.0.0-rc.1`)
   and injects it via `-p:Version=...` — the `<Version>` in
   `src/Directory.Build.props` is the dev-time baseline only; the tag wins.
2. Builds `Jaunty.slnx` in Release, runs the full test suite, packs the 7
   shippable packages (benchmarks/samples/SourceGenerator are IsPackable=false).
3. Pushes `.nupkg`s to **GitHub Packages**
   (`https://nuget.pkg.github.com/extrode/index.json`) using the workflow's
   own `GITHUB_TOKEN` — no secrets to manage.
4. Creates a GitHub Release with the `.nupkg` + `.snupkg` files attached
   (GitHub Packages has no symbol server; symbols ship on the Release).

Package validation (`EnablePackageValidation`) runs during pack, so a
ns2.0/net8.0 public-API divergence fails the release instead of shipping.

## Shipping v1.0.0-rc.1

```bash
# 0. Everything releasable is merged to dev and pushed; tree clean
git checkout dev && git pull

# 1. Full gate on the exact commit you will ship
dotnet build -c Release -warnaserror
dotnet test                       # both Jaunty.Tests TFMs run sequentially now

# 2. Fast-forward main to the release commit (main = what customers see)
git checkout main
git merge --ff-only dev
git push origin main

# 3. Tag main and push the tag — THIS is the release trigger
git tag v1.0.0-rc.1
git push origin v1.0.0-rc.1
```

Then watch the run: `gh run watch` (or Actions tab). On success verify:

- `gh release view v1.0.0-rc.1` shows the Release with nupkg + snupkg assets.
- Packages appear at github.com/extrode?tab=packages (7 packages, version
  1.0.0-rc.1, marked pre-release by NuGet clients automatically because of
  the `-rc.1` suffix).
- Smoke-consume from a scratch project (see next section).

## Consuming (you, a trial customer, or a paying customer)

GitHub Packages NuGet feeds are never anonymous — every consumer needs a PAT
with `read:packages` (fine-grained: "Packages: read"). This is the access
lever the commercial flow relies on (see COMMERCIAL-DISTRIBUTION-FLOW.md).

`nuget.config` next to the consumer's solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="jaunty" value="https://nuget.pkg.github.com/extrode/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <jaunty>
      <add key="Username" value="%JAUNTY_FEED_USER%" />
      <add key="ClearTextPassword" value="%JAUNTY_FEED_TOKEN%" />
    </jaunty>
  </packageSourceCredentials>
</configuration>
```

```bash
dotnet add package Jaunty --version 1.0.0-rc.1
```

## If something goes wrong

- **Workflow failed before publish**: fix on dev, delete the local+remote
  tag (`git tag -d v1.0.0-rc.1 && git push origin :refs/tags/v1.0.0-rc.1`),
  re-merge main, re-tag. Nothing shipped, so reusing the version is fine.
- **Bad package already published**: do NOT reuse the version. GitHub
  Packages allows deleting versions (repo Packages page or
  `gh api -X DELETE`), but any consumer may have cached it — ship
  `v1.0.0-rc.2` instead and delete/deprecate the bad one.
- A GitHub Release can be deleted independently of the tag; the tag
  independently of the package. Clean up all three when retracting.

## Promoting rc to stable

When rc.N has soaked: bump `<Version>` in `src/Directory.Build.props` to
`1.0.0` on dev (keeps dev builds honest), merge to main, tag `v1.0.0`.
Same pipeline, no other changes. After that, SemVer discipline per
CHANGELOG.md: breaking = major, additive = minor, fixes = patch.

## Pre-release TODO (tracked)

- [ ] **Branch protection for `main`** — blocked on GitHub Free for private
  repos. Decide: GitHub Pro (~$4/mo), Team org, or accept unprotected until
  the repo goes public/org. (Deferred 2026-07-04.)
- [ ] Delete leftover local container `jaunty-mssql3` when no longer needed.
