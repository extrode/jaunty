# Pricing

**Jaunty is free.** Free to use, including in commercial production, with no seat count, no Order and
no expiry. What is sold is **support**.

**JauntyQ is partly free.** A free core, with the migration and analysis capabilities and support
sold on top.

> **Status: the model is decided, the numbers are not.** The tables below carry the figures from the
> previous subscription model, repriced against what they now buy. They need the owner's sign-off
> before this page is published. The JauntyQ free/paid boundary is not drawn yet - see
> [decision 004](../decisions/2026-08-29-004-product-and-pricing-decision.md).

---

## Jaunty

| | |
|---|---|
| **Use it in production** | Free. Any company, any size, any number of developers |
| **All packages** | Free, public on NuGet |
| **Source** | Public and readable under ISL-R |
| **Updates** | Free |
| **Support** | Paid. See below |

There is no trial, because there is nothing to trial. There is no lapse, because nothing expires.

**The ethical restrictions still apply.** ISL sections 4 and 5 are conditions of the licence grant,
not of payment. A user who pays nothing is bound by them exactly as a paying one is. Free changes the
price; it does not change what the licence asks of you.

## Support subscriptions

What a support subscription buys is a response, not a right to run.

| Tier | Price | What it includes |
|---|---|---|
| **Community** | Free | Public issue tracker, best effort, no commitment |
| **Company** | *(pending - was $279/dev/yr as a licence)* | Priority email support, named contact, defect triage ahead of the public queue |
| **Enterprise** | *(pending - from $7,600/yr as a licence)* | Response-time SLA, escrow and continuity rider, priority fixes and hotfix backports, invoice terms, an entity to hold to it |

**The Enterprise agreement is the product.** It is what organisations buy from software they could
otherwise use for nothing: someone answering the phone, a defined turnaround, and a continuity rider
granting contingent internal build-and-patch rights if the vendor ceases operations. That rider is
drafted (`continuity-rider-template.md`) and never depended on the price model.

**Individual support is not offered.** A solo developer does not buy an SLA, and pretending otherwise
puts a tier on the page that nobody purchases.

## JauntyQ

JauntyQ is a separate product: SQL-first, with an incremental compilation pipeline, rowversion
optimistic concurrency, migration intelligence, a Roslyn performance analyzer and no-box Npgsql
parameters.

| | |
|---|---|
| **Free core** | Yes. Boundary not yet drawn |
| **Paid** | Migration intelligence is the flagship. Schema diff, impact classification and plan analysis are the candidates around it |
| **Roslyn analyzer** | Free |
| **Support** | Paid, same shape as above |

JauntyQ is not public and has no published prices. Nothing on this page commits to one.

## How licensing works

- **Jaunty's packages and source** are governed by the **Islamic Software License - Restricted
  (ISL-R)**. Section 2 grants a royalty-free right to use, including internal commercial use, to any
  compliant licensee. That is the grant this page now matches.
- **The instrument for free packages still needs settling.** ISL-EULA is written around an Order with
  fees; shipping free binaries is either ISL-R directly or a zero-fee Order. This is a drafting
  question for the same lawyer handling the CLA.
- **JauntyQ's paid components** ship under the **ISL-EULA** with the grant conditioned on an Order.
- **Enterprise continuity** is a rider on the support agreement granting contingent, internal-only
  rights to build and patch, triggered only by defined events such as the vendor ceasing operations.

## Terms worth knowing

- **Seat:** one named developer, for support tiers only. CI and build agents do not consume seats.
- **Lapse:** a support subscription that ends stops the support. It does not stop the software - you
  keep using Jaunty for free, as everyone does.

To arrange support or discuss an Enterprise agreement, contact Extrode LLC via
<https://extrode.com/jaunty>.
