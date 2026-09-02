# Jaunty Redistribution Exception, Version 1.0

**Effective Date:** August 2026

**Copyright (c) 2026 Extrode LLC. All rights reserved.**

**What this document is for.** Jaunty is a library. Applied without qualification, the licence it
ships under would defeat it: ISL-R Section 2 prohibits Distribution of "the Software or any copy
thereof to any third party" without prior written consent, and a .NET library is distributed by
its ordinary use — every deployment of an application that references Jaunty ships a copy of
`Extrode.Jaunty.dll` inside it. Under the previous paid model that act was authorised by an Order
under the ISL-EULA. Jaunty is now free to use with no Order, so nothing authorises it. This
Exception is the carve-out that does, and it is what makes the free grant operative rather than
nominal. **The ethical restrictions travel with the redistributed binaries by design** — this is
not an unrestricted redistribution permission, and that limitation is deliberate.

**Which licence this modifies.** This Exception is an additional permission under the Islamic
Software License, Restricted, version 1.2 ("ISL-R"), the licence governing Jaunty's source and
its published packages. It modifies those terms only as stated below; everything not stated
remains governed by ISL-R.

**Relationship to the ISL-EULA.** Jaunty's `LICENSE-EULA.md` was the instrument of the paid,
Order-conditioned model that decision 002 retired. It does not govern Jaunty as distributed under
the free model. It was removed from the repository on 2026-09-02 and remains in the git history
for the historical record and for any pre-existing Order. Where a Licensee holds no Order, ISL-R
as modified by this Exception is the whole of the grant.

---

## 1. Definitions

**1.1 "Packages"** means the compiled assemblies published by the Licensor under the
`Extrode.Jaunty.*` package identifiers, in unmodified object form, together with their symbol and
documentation files.

**1.2 "Licensee Application"** means a work of the Licensee's own that references one or more
Packages, and whose functionality is not primarily a substitute for the Software itself.

**1.3** Terms defined in ISL-R and not redefined here carry the meaning given in ISL-R.

## 2. Grant of redistribution

Notwithstanding the prohibition on Distribution in ISL-R Section 2 (the lettered list of
prohibited acts beginning "The following are expressly prohibited", not the lettered grants
earlier in that Section), the Licensee may reproduce and distribute the Packages, in unmodified
object form only, as incorporated into or deployed alongside a Licensee Application, in any
medium and by any means, including as part of a container image, an installer, a published
artifact, or a hosted service offered to the Licensee's own customers.

No obligation to disclose the source of the Licensee Application, and no obligation to reproduce
the ISL-R text within the Licensee Application, arises from this Exception. ISL-R Section 3.1
(Attribution) continues to apply to the Licensee's own use.

## 3. What is not granted

This Exception grants no right to distribute the Packages other than as a component of a Licensee
Application. In particular it does not permit republishing the Packages to a package registry,
offering them for download as a library, mirroring them, or distributing them modified. It grants
no rights over the source code beyond the View right in ISL-R Section 2(b), and no right to
create Derivative Works.

## 4. Continuing ethical conditions

The rights granted by this Exception are expressly conditioned on ISL-R Section 4 (Ethical Use
Restrictions) and Section 5 (Genocide, Injustice, and State-Level Restrictions), which continue
to apply to the Licensee's use and distribution of the Packages, including their use within any
Licensee Application. A Licensee Application must not be created, operated, offered, or
distributed in service of a Prohibited Activity. Violation terminates this Exception together
with ISL-R, per ISL-R Section 7.

## 5. Scope of downstream obligation

This Exception binds the Licensee. Recipients of a Licensee Application in object form do not
thereby become licensees of the Software, are not required to accept ISL-R, and the Licensee is
not required to impose its terms on such recipients. A recipient who extracts the Packages from a
Licensee Application and uses them other than as part of that application receives no rights from
this Exception.

## 6. No fee, no term

This Exception is royalty-free and does not expire. It is not conditioned on an Order, a
subscription, a seat count, or the purchase of support. A support subscription that lapses has no
effect on the rights granted here.

## 7. Severability and precedence

If any provision of this Exception is held unenforceable, it shall be modified to the minimum
extent necessary to make it enforceable and the remainder shall continue in force. Where this
Exception and ISL-R conflict as to distribution of the Packages, this Exception controls; in
every other respect ISL-R controls.

---

## Notes for legal review

Recorded rather than resolved, so a reviewer sees what the project already knows is soft:

1. **Section 1.2's "not primarily a substitute for the Software"** is borrowed from the JauntyQ
   Generated Output Exception Section 1.3 and carries the same weakness: it expresses the intent
   to stop the Exception being used to ship a repackaged Jaunty as a competing micro-ORM, but the
   wording is not tight. A reviewer should sharpen it, and should confirm it does not accidentally
   catch a legitimate application that happens to expose data access to its own users.
2. **Section 5's downstream depth** matches the choice already made for JauntyQ (Beparey,
   2026-08-17): the ethical restrictions bind the Licensee's creation, operation and distribution
   of their application, but do not reach the end users of that application. Consistency between
   the two products was the reason for following it here; the reasoning itself is recorded in
   `LICENSE-OUTPUT-EXCEPTION.md` in the JauntyQ repository.
3. **Whether ISL-R should carry a redistribution permission natively** rather than each library
   product bolting one on is a question for the ISL authors. Two Extrode products have now needed
   a rider for the same structural reason — ISL-R was drafted for software that is run, not for
   software that is linked. A reusable "ISL Linking Exception" would serve both and anyone else
   licensing a library under ISL-R.
4. **The retained `LICENSE-EULA.md`** created an interpretive risk: a reader could take the
   presence of a EULA in the repository as evidence that operational use requires an Order.
   Resolved 2026-09-02 by removing the file from the tree; it remains in history.

*This document was drafted by the project, not by counsel. It should be reviewed by a qualified
attorney — and, for the ethical-scope questions, referred to qualified scholars — before
publication.*
