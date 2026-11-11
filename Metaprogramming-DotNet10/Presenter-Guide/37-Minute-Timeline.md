# 37-Minute Timeline

Cumulative elapsed time, per slide. The column that matters when you are on stage is
**"clock at end"** — glance at it, not at the per-slide budget.

## Section budget (as the syllabus specifies)

| Section | Duration | Notes |
|---|---|---|
| Part 1 — Understanding Metaprogramming | **6:00** | includes the 1:30 opening |
| Part 2 — Runtime Introspection | **10:00** | |
| Part 3 — Runtime Code Generation | **12:00** | the largest block, deliberately |
| Part 4 — Real-World Applications | **9:00** | includes the 1:30 closing |
| **Total** | **37:00** | |

The introduction and closing are absorbed inside Parts 1 and 4, exactly as the syllabus
requires.

---

## Per-slide timeline

| # | Slide | Budget | Clock at end | Section |
|---|---|---|---|---|
| 1 | Title | 0:45 | **0:45** | Opening |
| 2 | Agenda and session objectives | 0:45 | **1:30** | Opening |
| 3 | What is metaprogramming? | 1:00 | **2:30** | Part 1 |
| 4 | Normal programming vs metaprogramming | 1:00 | **3:30** | Part 1 |
| 5 | Why do we need it? Start with the problem | 0:50 | **4:20** | Part 1 |
| 6 | Benefits — and the three stages | 1:00 | **5:20** | Part 1 |
| 7 | Implicit vs explicit metadata | 0:40 | **6:00** | Part 1 ✅ |
| 8 | What is runtime introspection? | 0:50 | **6:50** | Part 2 |
| 9 | Understanding .NET metadata | 1:20 | **8:10** | Part 2 |
| 10 | The Reflection API | 1:10 | **9:20** | Part 2 |
| 11 | Assembly and type discovery | 1:20 | **10:40** | Part 2 |
| 12 | Inspecting members — and BindingFlags | 1:20 | **12:00** | Part 2 |
| 13 | Dynamic object creation — Activator | 1:20 | **13:20** | Part 2 |
| 14 | Attributes and custom attributes | 1:30 | **14:50** | Part 2 |
| 15 | Practical example — plugin discovery | 1:10 | **16:00** | Part 2 ✅ |
| 16 | What is runtime code generation? | 1:00 | **17:00** | Part 3 |
| 17 | Reflection vs Reflection.Emit | 1:20 | **18:20** | Part 3 |
| 18 | Introduction to CIL / MSIL | 1:30 | **19:50** | Part 3 |
| 19 | IL instructions and the evaluation stack | 2:00 | **21:50** | Part 3 |
| 20 | DynamicMethod, ILGenerator, CreateDelegate | 1:40 | **23:30** | Part 3 |
| 21 | Practical example — generating a method | 2:00 | **25:30** | Part 3 |
| 22 | AssemblyBuilder → MethodBuilder | 1:30 | **27:00** | Part 3 |
| 23 | Generating types and methods at runtime | 1:00 | **28:00** | Part 3 ✅ |
| 24 | ASP.NET Core MVC and dependency injection | 2:15 | **30:15** | Part 4 |
| 25 | Entity Framework Core and serialization | 2:00 | **32:15** | Part 4 |
| 26 | Plugin architectures and dynamic proxies | 1:30 | **33:45** | Part 4 |
| 27 | Cross-cutting concerns and expression trees | 1:45 | **35:30** | Part 4 |
| 28 | Key takeaways, Q&A, references | 1:30 | **37:00** | Closing ✅ |

---

## The four checkpoints

Put these on a sticky note. If you only track four numbers, track these.

| Checkpoint | Clock | If you are LATE |
|---|---|---|
| **End of Part 1** (leaving slide 7) | **6:00** | Cut slide 5's "be fair about the alternatives" box to one sentence. |
| **End of Part 2** (leaving slide 15) | **16:00** | **Skip the plugin demo.** Talk over the diagram instead. |
| **End of Part 3** (leaving slide 23) | **28:00** | Compress slide 23 to 30 s — the heavy lifting was slide 22. |
| **End of Part 4** (leaving slide 27) | **35:30** | Cut slide 25's JSON measurement box entirely. |

> **The rule if you are behind at 16:00: cut from Part 2, never from Part 3.**
> Part 2 is the part people can read up on afterwards. Part 3 is the part they came for,
> and it is where the material most often found online is wrong.

---

## What to cut, in order

Work down this list. Each line is independently droppable without breaking anything
later.

| Order | Cut | Saves | Cost of cutting |
|---|---|---|---|
| 1 | Slide 25's `JsonSerializerOptions` measurement box | ~25 s | low — it is a refinement, not the point |
| 2 | Slide 26's three plugin bullets (slide 15 covered them) | ~20 s | low |
| 3 | Slide 12's `NonPublic` discussion, keeping only "private is not security" | ~25 s | low |
| 4 | Slide 10's `MemberInfo` / navigability side boxes | ~20 s | low |
| 5 | The **plugin demo** on slide 15 | ~40 s | medium — it is the Part 2 payoff |
| 6 | Slide 5's alternatives box, down to one sentence | ~15 s | medium — it buys you credibility |
| 7 | Slide 18's ReadyToRun / tiered-compilation asides | ~20 s | medium |

**Never cut:**

- slide 19 (the evaluation stack) — it is the foundation for 20–23;
- the `DynamicMethod` demo on slide 21 — it is the single best demo in the session;
- the "ALC is not a sandbox" line on slide 15;
- the `AssemblyBuilderAccess` correction on slide 22;
- slide 28's five takeaways.

---

## If you are running EARLY

More likely than you think, because the diagram slides go fast. In priority order:

1. **Slide 19** — walk the evaluation stack a second time, backwards. It genuinely helps.
2. **Slide 21** — run the demo again in Debug and let them see the timings change.
3. **Slide 11** — show `typeof(X).AssemblyQualifiedName` live and read the string aloud.
4. **Slide 24** — run `10-ASPNetCoreMVC --selftest` and show `/api/metadata/actions`.
5. **Slide 25** — run the EF Core example and show `ToQueryString()` output live.

Each is worth 40–60 seconds and adds real value rather than padding.

---

## Pacing notes

- **Diagram-only slides go faster than you expect.** Slides 9, 13, 15, 19, 20, 22, 24
  and 26 have almost no body text, so there is nothing to read out. Resist the urge to
  fill — point, explain the mechanism, move.
- **Slides 14, 21, 24 and 25 are the dense ones.** They are the ones that overrun. Know
  in advance which sentence you will drop from each.
- **Budget your demos at double what they take in rehearsal.** A terminal that will not
  focus, a build that decides to restore packages, a font too small to read — all of it
  costs time you did not plan for. See
  [`Live-Demo-Guide.md`](Live-Demo-Guide.md).
- **The 2:00 slides need a mid-point.** On slides 19 and 21, pick a sentence that marks
  halfway and check the clock there.

## Speaking pace

The script in [`Full-Presentation-Script.md`](Full-Presentation-Script.md) is written to
**≈140 words per minute**, which is a normal, unhurried conference pace for technical
content. At that rate 37 minutes is roughly **5,200 spoken words**, and the script is
sized to fit — the per-slide word budgets are printed in it so you can check any slide
you are unsure about.

If you naturally speak faster (160+ wpm), you will finish early; use the "running early"
list rather than adding material you have not rehearsed.
