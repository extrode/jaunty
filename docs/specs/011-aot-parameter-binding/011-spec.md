# AOT-Safe Parameter Binding — Feature Specification

> spec.md — The "what" and "why". No technical implementation details.

Status: implemented · Created: 2026-07-30 · Origin: round-27 carry-forward item 17, raised while
measuring [spec 009](../009-aot-annotation-pass/009-spec.md)

---

## 1. Problem Statement

Jaunty binds a parameters object by reflecting over its public properties. On a NativeAOT publish the
trimmer removes those getters, and binding fails at runtime:

```
anonymous  : FAIL -> No property found on type '<>f__AnonymousType0`1' matching '@Id'. Available properties:
named POCO : FAIL -> No property found on type 'ProductQuery'          matching '@Id'. Available properties:
no params  : OK
```

`Available properties:` is **empty** in both failing cases — every getter is gone. Measured on
`samples/NativeAOT-Basic`, published with `PublishAot=true`, 2026-07-30.

This is the same class of defect as spec 009 and was found by measuring it: 009 fixed mappers and
binders, and the shipped sample then failed one line later, on `new { Id = 1 }`. Spec 009 §6
explicitly excluded `ParameterCache`, so the exclusion was honoured and this was recorded rather than
quietly absorbed — which left 009's US-1 ("a published sample runs") unreachable by 009 alone.

## 2. Why This Is Not Spec 009's Remedy Applied Again

Spec 009's fix was for the generated entity to hold a **static method group reference** to its own
mapper, so the trimmer had an ordinary IL call to honour. Every part of that is unavailable here:

- A parameters object is **not an entity**. Jaunty has never been told the type exists.
- It is usually **anonymous**, so there is no name to write in an attribute, an interface
  implementation, or a `[DynamicDependency]` — and the consumer cannot annotate it either.
- There is **nothing to reference statically**. The members needed are the consumer's own property
  getters, not something a generator emitted.

The escape hatch that works for a hand-written `IMapped<T>` — "the consumer roots their own type" —
also does not apply. It does not happen automatically even for a named type the consumer owns
(measured), and it is impossible for an anonymous one.

## 3. Goals

- **G1.** An ordinary `connection.QueryFirst<Product>(sql, new { Id = 1 })` works in a NativeAOT
  published binary, with **no change to consumer source**.
- **G2.** The same for a named parameters POCO.
- **G3.** Where that is impossible, the consumer is told **at build time**, at the call site, with a
  message naming the fix — not left to discover it after publish.
- **G4.** Zero trim or AOT analyzer warnings attributable to Jaunty in a published application.
- **G5.** No public API is changed. Anything added is additive, and works on every target framework.

## 4. Non-Goals

- Making `Jaunty.Fluent` predicates over `decimal` work under AOT. That failure happens in the
  consumer's own expression-tree construction, before Jaunty is called at all (round-27
  carry-forward item 18).
- Threading a generic parameter type through the library's 170 `object? parameters` signatures. That
  would propagate annotations properly, and it is a breaking API change for no benefit over the
  approach taken here.
- Rooting parameter types Jaunty cannot see at a call site. See §6.

## 5. User Stories and Acceptance Criteria

**US-1 — the shipped sample runs.** `samples/NativeAOT-Basic`, published with `PublishAot=true` and
**executed**, completes every operation including the one taking `new { Id = 1 }`.

> A clean build is explicitly not sufficient, and this is the criterion 009 established for a
> reason: the broken state produced a clean build, a silent analyzer and a green test suite. The
> acceptance evidence is program output.

**US-2 — nothing attributable to Jaunty warns.** A published AOT application reports zero `IL2xxx`
and `IL3xxx` warnings originating in a Jaunty assembly. Any remaining suppression states a mechanism
that is true, not a defect that is tolerated.

**US-3 — no regression.** The full suite matches its recorded baseline; every target framework still
builds; consumers below net5.0 are unaffected.

**US-4 — the mechanism cannot rot silently.** The thing that makes this work is a single attribute.
Deleting it leaves everything compiling and every test green while the published binary breaks again.
It is pinned against compiled metadata.

## 6. Scope Boundary — and What It Cannot Reach

This works from **static types at call sites**, which is the only place a parameters type is
knowable. Two shapes are therefore out of reach, and both are reported or documented rather than
hidden:

| Shape | Outcome |
|---|---|
| `object p = new { Id = 1 }; conn.Query(sql, p)` | Reported at build time (§3 G3) |
| A consumer method taking `object`/`TParam` and forwarding it | **Not** reported. Invisible from both ends: the forwarding site has no concrete type, and the outer call is to the consumer's own method. Needs one explicit registration call. |

The second is a real gap and is documented on the public API rather than papered over. Reporting it
was tried and reverted: Jaunty's own generic write plumbing forwards an entity into an
observation-only `parameters` argument, which is the identical syntactic shape and entirely benign,
so telling the two apart would mean guessing.

## 7. Open Questions

1. **Should the diagnostic be gated on the consumer's `PublishTrimmed`/`IsTrimmable`?** It currently
   fires for JIT-only consumers who will never trim. Same question as JAUNTYGEN002, and the same
   answer for now: the condition it reports is real, so gate it via a `CompilerVisibleProperty` if it
   proves noisy — do not lower the severity. **Deferred, not resolved.**
2. **Does over-rooting cost anything measurable?** The detector matches by parameter name, so it also
   roots types passed to observation-only APIs. Over-rooting cannot produce a wrong binding, only a
   slightly larger binary. Unmeasured; recorded as unmeasured.
