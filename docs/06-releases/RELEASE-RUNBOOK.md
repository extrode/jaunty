# Release Runbook

How to ship a Jaunty release. The pipeline is fully wired: pushing a version
tag is the release action, and everything else here is verification around
that one step. The commands below take the version from `$VERSION`, so they
are the same commands for every release; the worked example is `1.0.0-rc.2`,
the version cut as of 2026-09-03.

## How the pipeline works

`.github/workflows/release.yml` triggers on any tag matching `v*`:

1. Derives the package version from the tag (`v1.0.0-rc.2` -> `1.0.0-rc.2`)
   and injects it via `-p:Version=...` — the `<Version>` in
   `src/Directory.Build.props` is the dev-time baseline only; the tag wins.
2. Builds `Jaunty.slnx` in Release, runs the full test suite, packs the 9
   shippable packages (benchmarks/samples/SourceGenerator are IsPackable=false).
3. Pushes `.nupkg`s to **GitHub Packages**
   (`https://nuget.pkg.github.com/extrode/index.json`) using the workflow's
   own `GITHUB_TOKEN` — no secrets to manage.
4. Creates a GitHub Release with the `.nupkg` + `.snupkg` files attached
   (GitHub Packages has no symbol server; symbols ship on the Release).

Package validation (`EnablePackageValidation`) runs during pack, so a
ns2.0/net8.0 public-API divergence fails the release instead of shipping.

## Shipping a release

```bash
VERSION=1.0.0-rc.2                # the version being shipped

# 0. Everything releasable is merged to dev and pushed; tree clean
git checkout dev && git pull

# 1. Full gate on the exact commit you will ship
dotnet build -c Release -warnaserror
dotnet test                       # both Jaunty.Tests TFMs run sequentially now

# 2. Merge dev into main (main = what customers see)
git checkout main
git merge --no-ff dev
git push origin main

# 3. Tag main and push the tag — THIS is the release trigger
git tag "v$VERSION"
git push origin "v$VERSION"
```

`--no-ff` is the release merge per [`conventions.md`](../conventions.md): the merge
commit is the release point, and the tag goes on it. A fast-forward would leave no
commit that means "this is what shipped".

Then watch the run: `gh run watch` (or Actions tab). On success verify:

- `gh release view "v$VERSION"` shows the Release with nupkg + snupkg assets.
- Packages appear at github.com/extrode?tab=packages (9 packages at that version;
  a `-rc.N` or other pre-release suffix marks them pre-release to NuGet clients
  automatically).
- Smoke-consume from a scratch project (see next section).

## Consuming (you, a trial customer, or a paying customer)

GitHub Packages NuGet feeds are never anonymous — every consumer needs a PAT
with `read:packages` (fine-grained: "Packages: read"). This is the access
lever the *old* commercial flow relied on. That flow is retired: Jaunty is free to use in
commercial production and what is sold is support, so the feed token gates nothing commercial.
[`COMMERCIAL-DISTRIBUTION-FLOW.md`](COMMERCIAL-DISTRIBUTION-FLOW.md) is kept for the historical
record only and carries a staleness banner saying so.

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

The package IDs are `Extrode.`-prefixed — the unprefixed `Jaunty` IDs were the
rc.1 names and are not the ones on the feed:

```bash
dotnet add package Extrode.Jaunty --version 1.0.0-rc.2
```

## If something goes wrong

- **Workflow failed before publish**: fix on dev, delete the local+remote
  tag (`git tag -d "v$VERSION" && git push origin ":refs/tags/v$VERSION"`),
  re-merge main, re-tag. Nothing shipped, so reusing the version is fine.
- **Bad package already published**: do NOT reuse the version. GitHub
  Packages allows deleting versions (repo Packages page or
  `gh api -X DELETE`), but any consumer may have cached it — ship the next
  version instead and delete/deprecate the bad one.
- A GitHub Release can be deleted independently of the tag; the tag
  independently of the package. Clean up all three when retracting.

## Promoting rc to stable

When rc.N has soaked: bump `<Version>` in `src/Directory.Build.props` to
`1.0.0` on dev (keeps dev builds honest), merge to main, tag `v1.0.0`.
Same pipeline, no other changes. After that, SemVer discipline per
CHANGELOG.md: breaking = major, additive = minor, fixes = patch.

## Pre-release TODO (tracked)

- [ ] **Branch protection for `main`** — was blocked on GitHub Free for private
  repos (deferred 2026-07-04); unblocked when the repository went public on
  2026-09-03. Enable it.
