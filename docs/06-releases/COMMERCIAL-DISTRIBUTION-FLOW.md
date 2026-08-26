# Selling and Distributing Jaunty — The Complete Flow

Written 2026-07-04. Answers: how do I show the product off, how do I distribute it
after a sale, and can I give customers my private feed / repo / source?
Companion to [COMMERCIAL-ANALYSIS-REPORT.md](COMMERCIAL-ANALYSIS-REPORT.md).
License context: Jaunty ships under the proprietary Islamic Software License - Restricted (ISL-R) v1.0
(use permitted, modification/redistribution/sublicensing prohibited) — which is
already the right shape for commercial licensing.

## The flow at a glance

```
Prospect                Trial                    Sale                    Ongoing
--------                -----                    ----                    -------
public docs site   →    time-boxed access   →    provision access   →    renewals = access expiry
public samples repo     to the private feed      (feed token and/or      support channel + SLA
benchmarks doc          (or trial package)       source access)          security advisories
demo video/livecoding                            invoice + license       version upgrades
```

## 1. Showing the product off (pre-sale)

You need public marketing artifacts that don't give the product away:

- **Public docs/marketing repo or site.** The private repo stays private; publish a
  separate public repo (e.g. `extrode/jaunty-docs`) containing the README-grade docs,
  the API reference (DocFX output from `tools/Jaunty.DocsGenerator`), the measured
  benchmark results (BENCHMARKS-2026-07-04.md), and the CHANGELOG. This is your
  storefront and your SEO.
- **Public samples repo.** The three NativeAOT samples plus a couple of realistic
  apps (ASP.NET Core API with logging/interceptors wired) compile against the
  package but the package itself isn't included — prospects can read the code and
  watch a demo even before trial access.
- **Demo assets.** A 10-minute recorded walkthrough (strict mapping catching a bug
  live, source-gen + NativeAOT publish, the Scaffolding CLI generating entities from
  Northwind) does more than any feature table. For live sales calls, keep one demo
  solution with containers scripted (`data/` + the CI bootstrap scripts already in
  the repo make this nearly free).
- **Proof artifacts.** The benchmark report, the SECURITY.md policy, the test counts
  (4,500+), and the CI badge are enterprise-trust signals — link them prominently.

## 2. Trial (the bridge from demo to sale)

Options, in order of recommendation:

1. **Time-boxed private-feed access (recommended).** Issue a GitHub fine-grained
   PAT scoped to package read, with a 30-day expiry. When the trial lapses, the
   token dies by itself — no revocation chores. Cheap to run, identical experience
   to the paid product.
2. **Trial package on nuget.org.** Publish `Extrode.Jaunty.Trial` (or `-trial`
   suffix versions) publicly with a built-in time bomb or nag. More reach, but you
   maintain a second package identity and trial code paths. Only worth it if
   inbound volume gets high.
3. **Evaluation source access under NDA.** For enterprise security reviews that
   demand code audit before purchase — see §4; grant read access to a tagged
   snapshot, time-boxed.

## 3. Distribution after the sale

**Yes — giving customers access to your private NuGet feed is exactly how this is
done.** The mechanics, per option:

### GitHub Packages (recommended — you're already on GitHub)

- Publish from release.yml with the built-in `GITHUB_TOKEN` (no NUGET_API_KEY
  secret needed at all).
- Per customer, either:
  - **Fine-grained PAT (simplest):** you create a token scoped to `packages:read`
    on the Jaunty repo, expiry = license period. Customer puts it in their
    `nuget.config` / CI secrets. Renewal = you issue a new token.
  - **Org seat:** invite customer engineers to a `jaunty-customers` team with read
    access. Better audit trail, more admin per head.
- Customer consumption:

  ```xml
  <!-- nuget.config -->
  <packageSources>
    <add key="jaunty" value="https://nuget.pkg.github.com/extrode/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <jaunty>
      <add key="Username" value="anything" />
      <add key="ClearTextPassword" value="%JAUNTY_FEED_TOKEN%" />
    </jaunty>
  </packageSourceCredentials>
  ```

- **Important honesty note:** feed access controls *acquisition*, not *use*.
  Packages a customer already restored keep working forever after expiry; they just
  stop getting updates and support. That is the industry-standard deal (Telerik,
  Syncfusion, Duende all work this way) — enforcement of continued use is the
  license contract's job, not the feed's.

### Alternatives

- **Azure Artifacts** — nicer feed-scoped tokens and upstream proxying; adds an
  Azure DevOps org to run. Choose if customers are Azure-heavy.
- **Self-hosted (ProGet/Artifactory)** — full control, per-customer license keys at
  the feed level; you run a server. Overkill at current scale.
- **Public nuget.org + license enforcement** — zero distribution friction, the
  Duende model: the package is public, paying is a legal/licensing matter,
  optionally backed by a license-key check in code. Viable, but it makes the
  "private product" positioning moot.

## 4. Source access for customers

Also standard practice, three tiers — pick per deal size:

1. **No source (default tier).** They get packages + snupkg symbols + SourceLink.
   Because SourceLink and symbol packages are already wired, customers can step
   *through* Jaunty source in the debugger without any repo access — this covers
   90% of the "we need source" ask. (Note: SourceLink resolves against the GitHub
   repo, so it only works for people with repo read access — for no-source-tier
   customers, ship the snupkg and consider `EmbedAllSources` for a self-contained
   debugging story.)
2. **Read-only repo access (premium tier).** Invite named customer users to the
   private repo with Read permission, or — cleaner — maintain a `jaunty-source`
   mirror repo that receives only tagged release states (no work-in-progress
   branches, no issue history, no audit trail leakage). GitHub read access
   technically allows cloning; the ISL-R license is what forbids redistribution and
   derivative works. That combination (technical read + legal restriction) is
   normal "source-available commercial" practice.
3. **Source drop per release (escrow tier).** Attach a source tarball (or git
   bundle) of the tagged release to the private GitHub Release. Enterprises with
   vendor-continuity requirements often *prefer* this to live repo access, and it
   never exposes your working repo. This also satisfies source-escrow clauses
   without a third-party escrow agent for smaller deals.

## 5. License enforcement spectrum

- **Contract-only (recommended to start):** ISL-EULA + order form per customer (see order-form-template.md; source tiers additionally under ISL-R).
  Zero code, zero friction, standard for dev tooling at this scale.
- **License key, warn-only:** `JauntyConfig.LicenseKey = "..."` validated offline
  (signed payload, public key embedded), logs a warning when missing/expired.
  Duende-style; add when revenue justifies it. Never hard-fail production paths of
  a data-access library — that failure mode punishes your best customers during
  incidents.
- **Obfuscation / hard activation:** not recommended; hostile to enterprise build
  environments and trivially bypassed for .NET anyway.

## 6. Concrete recommended setup for Jaunty (solo vendor, today)

1. Keep `extrode/jaunty` private (product + source of truth).
2. Create public `jaunty-docs` (docs site + benchmarks + changelog) and
   `jaunty-samples` — the storefront.
3. Wire release.yml to GitHub Packages (`GITHUB_TOKEN`) — kills the NUGET_API_KEY
   manual step. Tag `v1.0.0` when the readiness list closes.
4. Trials: 30-day fine-grained PAT, `packages:read` only.
5. Sales: invoice + countersigned license (ISL-EULA + order form with seat count and
   term) → issue feed token with expiry = term → optionally add source tier.
6. Support: private per-customer GitHub Discussions/Issues in a
   `jaunty-support-<customer>` repo, or email with the SECURITY.md process for
   vulnerabilities. Put response-time targets in the order form, not the license.
7. Renewals: token expiry does the reminding; lapsed customers keep what they
   restored, lose updates/support — spell this out in the order form.

## Decisions still open

- Distribution model itself (deferred 2026-07-04): GitHub Packages vs public
  nuget.org + licensing. Everything in §6 assumes GitHub Packages; if public
  nuget.org wins later, §3 collapses to "publish and enforce via license" and
  trials become suffix packages.
- ~~Pricing/tiers, seat vs org licensing, source-access tiers~~ DECIDED 2026-07-05: see pricing.md (seat-based, 3 tiers, 20% launch discount to 2026-10-05), order-form-template.md, continuity-rider-template.md. Was:
  business calls, not wired into anything technical yet.
