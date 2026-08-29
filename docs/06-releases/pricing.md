# Pricing

**Jaunty is free.** Free to use, including in commercial production, with no seat count, no Order and
no expiry. What is sold is **support**.

**JauntyQ is partly free.** A free core, with the migration and analysis capabilities and support
sold on top.

> **Status: Jaunty is settled, JauntyQ is not.** Jaunty's licence instrument and support numbers were
> decided 2026-08-29 - see [decision 007](../decisions/2026-08-29-007-jaunty-licence-instrument-and-support-pricing.md).
> JauntyQ's free/paid boundary and price remain open; the figures in its section below are
> indicative and commit to nothing. See
> [decision 006](../decisions/2026-08-29-006-jauntyq-value-and-pricing-panel.md).

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
| **Company** | **$1,200 per organisation, per year** | Priority email support, named contact, defect triage ahead of the public queue. Any number of your developers may file |
| **Enterprise** | **From $7,500 per year** | Everything in Company, plus a response-time SLA, the escrow and continuity rider, priority fixes and hotfix backports, and invoice terms |

**Company is per organisation, not per developer.** The old $279/dev/yr was the price of a licence,
and a licence is consumed per developer. Support is consumed per question. Counting developers to
price email from a small vendor creates an audit problem for something with no enforcement surface,
and a forty-developer shop was never going to pay $11,000 for it. One number, no counting, below the
threshold that sends a purchase to procurement.

**Enterprise stays near the old figure** because $7,600 was already the price of the SLA and the
rider rather than of the right to run, and that is what it still buys. It is a "from" price: the
response times and the rider's trigger events are negotiated per agreement. **A small number are
taken at a time** - an SLA a single vendor cannot service is worse than no SLA offered.

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
  compliant licensee. That is the grant this page matches.
- **Shipping Jaunty inside your application** is permitted by the **Jaunty Redistribution
  Exception**, a royalty-free rider on ISL-R with no term and no Order. ISL-R alone forbids
  distributing the software to third parties, which every deployment of an application that
  references a library does; the Exception is what makes the free grant usable. The ethical
  restrictions travel with the redistributed binaries.
- **Jaunty's ISL-EULA does not apply** under the free model. It was the instrument of the paid,
  Order-conditioned model and is retained for the historical record.
- **JauntyQ's paid components** ship under the **ISL-EULA** with the grant conditioned on an Order.
- **Enterprise continuity** is a rider on the support agreement granting contingent, internal-only
  rights to build and patch, triggered only by defined events such as the vendor ceasing operations.

## Terms worth knowing

- **Seat:** one named developer, for support tiers only. CI and build agents do not consume seats.
- **Lapse:** a support subscription that ends stops the support. It does not stop the software - you
  keep using Jaunty for free, as everyone does.

To arrange support or discuss an Enterprise agreement, contact Extrode LLC via
<https://extrode.com/jaunty>.
