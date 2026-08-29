# Jaunty Contributor License Agreement (CLA)

**Version 1.0-draft — 2026-08-29**

> **DRAFT — NOT YET IN FORCE.** This document has not been reviewed by counsel. It states the
> intended terms so that they can be reviewed and finalised. Do not rely on it, and do not accept
> contributions under it, until Extrode LLC marks it final. Tracked as task T5 in
> `docs/plans/2026-08-29-004-public-release.md`.

---

## Why this document exists

Jaunty's source is published under the **Islamic Software License – Restricted (ISL-R) v1.0**
(`LICENSE.md`). ISL-R Section 2 permits use and viewing of the source, and prohibits modification
and derivative works — **"without prior written consent from the Licensor."**

That final clause is the whole basis of this agreement. **A signed CLA is that prior written
consent.** It is what makes contributing lawful without changing the licence, and without weakening
the Ethical Use Restrictions in ISL-R Sections 4 and 5, which continue to bind everyone.

Without a signed CLA, preparing a patch to Jaunty is a modification ISL-R does not permit. With
one, it is expressly authorised, for the limited purpose described below.

---

## 1. Definitions

**"Project"** means Jaunty and the other software in the `extrode/jaunty` repository.

**"Licensor"**, **"we"**, **"us"** means Extrode LLC, the copyright holder of the Project.

**"You"** means the individual or legal entity agreeing to these terms. Where You agree on behalf of
an employer or other entity, You represent that You are authorised to bind that entity.

**"Contribution"** means any work of authorship You intentionally submit to the Project for
inclusion — source code, documentation, tests, configuration, or other material — by pull request,
patch, issue attachment, email, or any other means.

**"Submit"** means any form of communication sent to the Licensor or its representatives, excluding
communication conspicuously marked in writing as "Not a Contribution."

---

## 2. Limited consent to modify

Subject to Your compliance with this Agreement, the Licensor grants You the **prior written consent
contemplated by ISL-R Section 2** to reproduce and modify the Project **solely** for the purpose of
preparing and submitting a Contribution.

This consent is:

- **limited** — it extends only to preparing Contributions, and to the private copies and forks
  necessary to do so;
- **non-transferable** — it authorises You, not anyone you give your copy to;
- **revocable** — the Licensor may withdraw it at any time on written notice; and
- **not a grant of any right to distribute** the Project or any modified version of it to third
  parties. Publishing a modified Jaunty remains prohibited by ISL-R.

Every other term of ISL-R, including the Ethical Use Restrictions in Sections 4 and 5, continues to
apply to You in full.

> **Note on GitHub forks.** Making a fork on GitHub in order to open a pull request is authorised by
> this Section. Publishing a modified Jaunty as your own product is not, and never becomes so. See
> `CONTRIBUTING.md` for what this means in practice.

## 3. Copyright licence to the Licensor

You grant the Licensor and its successors a **perpetual, worldwide, non-exclusive, royalty-free,
irrevocable** licence to reproduce, prepare derivative works of, publicly display, publicly perform,
sublicense, and distribute Your Contribution and any derivative works of it.

**This grant is deliberately broad, and You should understand what it allows.** The Licensor
distributes Jaunty's source under ISL-R and its binaries commercially under the ISL-EULA. This
licence is what permits Your Contribution to be included in **both**, including in paid commercial
releases, without any further permission from You and without any payment to You.

You retain full ownership of Your Contribution and remain free to use it elsewhere however You wish.

## 4. Patent licence

You grant the Licensor and every recipient of the Project a perpetual, worldwide, non-exclusive,
royalty-free, irrevocable patent licence to make, use, sell, offer to sell, import, and otherwise
transfer the Project, covering only those patent claims You own or control that are necessarily
infringed by Your Contribution alone or by its combination with the Project.

If any entity institutes patent litigation alleging that the Project or a Contribution constitutes
patent infringement, the patent licences granted under this Agreement to that entity terminate as of
the date the litigation is filed.

## 5. Your representations

You represent that, for each Contribution:

1. **It is Your original work.** You wrote it yourself.
2. **You have the right to grant the licences above.** If Your employer has rights in work You
   create, You have obtained permission, or Your employer has waived those rights, or Your employer
   has itself agreed to this Agreement.
3. **It is not encumbered.** It is not subject to any third-party licence, patent claim, or other
   obligation that would conflict with the grants in Sections 3 and 4. If any part of it is not Your
   own work, You have identified that part, its source, and its licence, conspicuously and in
   writing, when submitting.
4. **It complies with Section 6.**

You are not expected to provide support for Your Contribution, and unless required by applicable law
Your Contribution is provided **"AS IS", without warranties or conditions of any kind**.

## 6. No AI-generated contributions

**Contributions must be written by a human being. Contributions generated in whole or in part by an
artificial intelligence or machine-learning system are not accepted.**

This includes, without limitation, code produced by large language models, AI coding assistants,
code-completion systems that emit more than trivial single-token or single-identifier suggestions,
and any automated code generator trained on third-party source code.

By submitting a Contribution, You represent that **You personally authored it**, and that no such
system produced or substantially assisted in producing it.

### What you may do instead

This prohibition applies to **material submitted for inclusion**. It does not stop You participating:

| Welcome | Not accepted |
|---|---|
| Bug reports, in your own words | Patches written by an AI assistant |
| Reproduction steps and failing input | AI-generated tests or documentation |
| A prose description of a proposed design | AI-translated or AI-refactored code |
| A written explanation of a defect and where it is | Code you cannot explain line by line |

**Describe the problem or the change in writing and we will implement it.** A precise, well-argued
issue is more useful to this project than a patch, and it is the contribution route we prefer.

### Why

Three reasons, stated plainly so the rule does not look arbitrary:

1. **Provenance.** AI systems are trained on code under licences we cannot audit and whose terms may
   conflict with ISL-R and the ISL-EULA. We cannot accept a Contribution whose copyright status we
   are unable to establish, and neither Section 5 nor Section 3 can be honestly warranted for one.
2. **Accountability.** Every line in this project must have a human who understood it when it was
   written and can answer for it afterwards.
3. **The Licensor uses AI tooling itself, under its own review and responsibility.** That is a
   choice we make about our own copyright and our own liability. It is not one we can make on
   Your behalf, or on behalf of the code You send us.

We may ask You to confirm the authorship of a Contribution. A Contribution we believe to be
AI-generated will be declined, and we are not obliged to explain how we reached that view.

## 7. Ethical use restrictions

Nothing in this Agreement waives, narrows, or creates an exception to the Ethical Use Restrictions in
ISL-R Sections 4 and 5. They bind You as a contributor exactly as they bind any other licensee.

## 8. No obligation

The Licensor is under **no obligation** to review, accept, merge, or ship any Contribution, and may
decline any Contribution for any reason or none. Submitting does not make You an employee, partner,
agent, or joint venturer of the Licensor, and creates no entitlement to compensation, attribution
beyond the project's ordinary practice, or continued inclusion of Your Contribution in the Project.

## 9. Miscellaneous

**9.1 Notice of inaccuracy.** You agree to notify the Licensor promptly if You become aware that any
representation in Section 5 or Section 6 has become inaccurate.

**9.2 Governing law.** This Agreement is governed by the laws applicable to Extrode LLC, without
regard to conflict-of-laws principles. _[To be completed on legal review.]_

**9.3 Entire agreement.** This Agreement, together with ISL-R, is the entire agreement between You
and the Licensor concerning Contributions, and supersedes any prior understanding.

**9.4 Severability.** If any provision is held unenforceable, it is modified to the minimum extent
necessary to make it enforceable, and the remaining provisions stay in force.

**9.5 Versions.** The Licensor may publish revised versions of this Agreement. A Contribution is
governed by the version in force when it was submitted.

---

## How to sign

_[Mechanism to be confirmed on legal review — likely a CLA-assistant bot check on each pull request,
or a signed entry appended to `contributors.md`.]_

Until then, every pull request must include this statement, and it must be true:

```
I have read and agree to the Jaunty Contributor License Agreement (CLA.md).
I personally authored this contribution. No part of it was generated by an
AI or machine-learning system.
```
