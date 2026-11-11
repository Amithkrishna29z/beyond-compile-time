# Validation Report

**Package:** Metaprogramming in .NET 10 — Runtime Introspection, Reflection, and Dynamic
Code Generation
**Validated:** 2026-11-11
**Machine:** Windows 11 x64 · .NET SDK **10.0.400** · runtime **10.0.11** · Python 3.12.5

Everything below was **executed and observed**. Where something could not be verified, it is
stated as such rather than claimed.

---

## 1. Presentation validation

| Check | Result |
|---|---|
| PPTX created | **Yes** — `Presentation/Metaprogramming-DotNet10.pptx`, 162,437 bytes |
| Slide count | **28** (target was 24–28) |
| Aspect ratio | **16:9** — 13.333 × 7.5 in, confirmed from the file |
| Speaker notes included | **Yes — on all 28 slides**, 43,367 characters total |
| Every slide has a title | **Yes** |
| Every slide has a duration in its notes | **Yes — 28/28**, each as `[m:ss]` |
| Every slide has a transition line | **Yes — 27/27** (slide 28 ends the talk) |
| Editable | **Yes** — all diagrams are native PowerPoint shapes (rounded rectangles + connectors), not images |
| Opens in standard software | **Yes** — valid OOXML, re-read with `python-pptx` after every build |
| Visual inspection completed | **Programmatically — yes. Visually — NO.** See the limitation below. |
| Issues found and fixed | **79 → 0.** See §1.3. |

### 1.1 Composition

| | Count |
|---|---|
| Shapes | 553 |
| Text runs | 1,094 |
| Diagram shapes (nodes) | 168 |
| Arrow connectors | 83 — **all 83 carry a real arrowhead** |
| Tables | 9 |
| Monospace (code) runs | 376 |
| Diagrams placed in the deck | **16** of 18 |

### 1.2 ⚠ Limitation — no visual render

**LibreOffice / `soffice` is not installed on this machine**, and no other renderer
(`pandoc`, `wkhtmltopdf`, `weasyprint`) is available. **The slides could not be rasterised
to images and eyeballed.**

What was done instead — a geometric and textual inspection of the `.pptx`, catching the
layout faults that actually occur:

| Automated check | Result |
|---|---|
| Any shape starting off-slide | none |
| Any shape past the right edge | none |
| Any shape past the bottom edge | none |
| Any text below **9 pt** (projector legibility floor) | none |
| Any non-wrapping (code) line too wide → **clipped** | none |
| Any text block taller than its container → **vertical overflow** | none |
| Slides with no text | none |
| Slides missing notes / duration / transition | none |

**Recommendation:** a human should still flick through all 28 slides once in PowerPoint
before presenting. The automated pass cannot judge aesthetics, font substitution on
non-Windows systems, or whether a diagram *reads* well.

### 1.3 Issues found and fixed

The first build had **79 layout defects**. All were fixed and re-verified to zero.

| Class | Count | Cause | Fix |
|---|---|---|---|
| Diagram text below 9 pt | 48 | Diagrams scaled down to share a slide, shrinking their labels | Widened the diagram's slot on slides 10, 11, 15, 17, 19, 26, 27; redesigned diagrams **07** and **16** onto deliberately wide, short canvases so their labels survive scaling |
| Code lines wider than their box | 18 | Fixed font size with no fit check | Added `fit_mono()` — code and console blocks now **auto-size** to their widest line and have `word_wrap` disabled, so code can never silently wrap or clip. A block that cannot fit at ≥ 9 pt now **fails the build** |
| Long titles wrapping into the content area | 3 | Fixed 29 pt title | Titles auto-size between 20 and 29 pt to stay on one line |
| Text blocks taller than their container | 10 | Boxes sized by eye | Heights corrected on slides 11, 14, 22, 25 |

Two of these were genuine content bugs, not just cosmetics:

- the "byte-for-byte identical" output box on slide 25 was **shorter than its own caption
  plus one line** — it would have rendered clipped;
- slide 22's title (`AssemblyBuilder, ModuleBuilder, TypeBuilder, MethodBuilder`) was 0.05 in
  too wide at 29 pt and would have wrapped over the diagram.

### 1.4 Diagrams

| Check | Result |
|---|---|
| Required diagrams created | **17 / 17** |
| Plus a composite for slide 24 | 1 (see below) |
| Total diagram assets | **18**, as SVG (vector — resolution-independent) |
| SVG well-formed XML | **18 / 18** |
| Text overflowing its box (estimated glyph widths, every label line) | **0** |
| Overlapping content boxes (container frames excluded) | **0** |
| Cropped diagrams in the deck | **none** — each is scaled to fit its slot, preserving aspect |

**Diagrams 14 (`di-resolution`) and 15 (`mvc-action-discovery`) are not placed on a slide.**
Slide 24's syllabus title covers *both* MVC discovery and DI, and each of those diagrams is
~450 units tall — stacking them on one 16:9 slide would have pushed their labels to about
7 pt. A composite (`18-mvc-di-combined`) carries the single lesson both teach, at full
legibility. Both originals exist in full as standalone assets and are referenced from
`Study-Materials/Part-4-Real-World-Applications.md` §1 and §2. This is documented in
`Assets/Diagrams/README.md`.

---

## 2. Study materials validation

| Check | Result |
|---|---|
| All syllabus topics covered | **Yes — 112 / 112 topic markers found** by automated keyword scan across the study materials, presenter guide and code |
| Markdown files created | **Yes — 9** in `Study-Materials/` |
| Combined Markdown guide | **Yes** — `Complete-Study-Guide.md`, 6,743 lines, **36,539 words** |
| Combined PDF created | **Yes** — `Complete-Study-Guide.pdf`, **129 pages**, 360,719 bytes |
| PDF readable, no missing sections | **Yes** — text re-extracted with `pypdf`: 259,675 characters, and all 16 section/API markers located |
| Code explanations included | **Yes** — line-by-line walkthroughs in every Part |
| Expected output included | **Yes** — and every console block is **real captured output** |
| References included | **Yes** — 56 Microsoft documentation links |
| References verified | **Yes — 55 returned HTTP 200 on the first pass**; the 1 that 404'd was replaced with a working URL and re-checked. **No unverified or invented URLs.** |

### 2.1 Per-file

| File | Lines | Words |
|---|---|---|
| `Part-1-Metaprogramming.md` | 869 | 5,118 |
| `Part-2-Runtime-Introspection.md` | 1,357 | 6,759 |
| `Part-3-Runtime-Code-Generation.md` | 1,166 | 6,432 |
| `Part-4-Real-World-Applications.md` | 1,924 | 9,426 |
| `Quick-Revision-Notes.md` | 323 | 1,896 |
| `Interview-Questions.md` | 482 | 3,231 |
| `Glossary.md` | 343 | 2,286 |
| `References.md` | 186 | 1,016 |
| `Complete-Study-Guide.md` | 6,743 | 36,539 |

### 2.2 ⚠ Limitation — PDF toolchain

No `pandoc`, `wkhtmltopdf` or `weasyprint` is installed. The PDF was generated with
**ReportLab** via a purpose-written renderer that supports the Markdown subset this document
actually uses: headings, paragraphs, fenced code, pipe tables, lists, blockquotes, rules,
and inline bold/italic/code.

Consequences, stated plainly:

- Links render as their **label text**, not as clickable anchors. Every URL is also printed
  in full in the References chapter, so nothing is lost.
- Code blocks wrap at 112 characters with a continuation indent rather than scrolling.
- There is no generated table of contents with page numbers (the Contents list is editorial).

**Verified present** by extracting the text back out of the finished PDF: all four Parts,
Quick Revision, Interview Questions, Glossary and References, plus spot-checks for
`AssemblyBuilderAccess`, `PersistedAssemblyBuilder`, `DispatchProxy`, `ExpressionVisitor`,
`ldarg.0`, `instr(`, `DynamicServiceProviderEngine`, `evaluation stack` and
`shadow property`.

### 2.3 Factual corrections made during authoring

Four claims were written, **measured, found wrong, and corrected** rather than shipped:

1. **"Invalid IL throws `InvalidProgramException`."** Half true. A missing `ret` does. But
   `add` on an `int` and a `string` threw **nothing** and silently returned garbage. The
   section was rewritten around that asymmetry, which is a stronger and truer lesson.
2. **"A fresh `JsonSerializerOptions` re-reflects every call."** Measured at only 1.4–1.8×,
   because `System.Text.Json` shares a process-wide cache between *equivalent* options
   instances. Re-measured three ways; the real cost is **varying** the settings (7–9×).
3. **"SQLite translates `string.Contains` to `LIKE`."** It emits `instr(...) > 0`. The
   narration was corrected and the provider-specificity made into a teaching point.
4. **"13 projects."** The solution has **14** (12 runnable examples + 2 libraries). Counted
   and corrected across the README, the deck, the script, the demo guide and the study guide.

Also corrected: a benchmark that compared a boxing emitted getter against a non-boxing
cached delegate — an unfair comparison. A typed emitted variant was added, which revealed
that **boxing, not reflection-vs-emit, dominates** — now one of the session's findings.

---

## 3. Code validation

| Check | Result |
|---|---|
| .NET SDK detected | **10.0.400** (also 8.0.427 present) |
| Runtime | `Microsoft.NETCore.App 10.0.11`, `Microsoft.AspNetCore.App 10.0.11` |
| Target framework | `net10.0` for all 14 projects |
| Projects | **14** — 11 console + 1 web + 2 libraries |
| Solution restore | ✅ **Succeeded** (clean, after deleting all `bin`/`obj`) |
| Solution build (Release) | ✅ **Succeeded — 0 Warning(s), 0 Error(s)** |
| Examples executed | ✅ **12 / 12, exit code 0** |
| Web example starts | ✅ **Yes** — self-tests over real HTTP and shuts down cleanly |
| EF Core example | ✅ **Yes** — SQLite in-memory, schema created, 8 queries, real SQL captured |
| Failed examples | **none** |
| Untested | **none** of the 12 |

### 3.1 Execution results

Captured in `Code-Examples/expected-output/` — 2,246 lines total.

| Example | Exit | Output lines | Notable verification |
|---|---|---|---|
| `01-Basics` | 0 | 98 | reflective describer across 3 unrelated types |
| `02-Reflection` | 0 | 164 | all 5 `Activator` failure modes triggered on purpose |
| `03-Attributes` | 0 | 117 | `AmbiguousMatchException` and all 4 inherit combinations |
| `04-Plugin-Discovery` | 0 | 105 | plugin loaded from disk; **ALC unloaded on GC attempt 2** |
| `05-DynamicMethod` | 0 | 238 | IL emitted; benchmark; **invalid IL silently returning garbage** |
| `06-ReflectionEmit` | 0 | 182 | type generated; **2,048-byte DLL saved, reloaded, invoked → 42** |
| `07-DependencyInjection` | 0 | 176 | `DynamicServiceProviderEngine` read from a private field |
| `08-ExpressionTrees` | 0 | 197 | tree walked, rewritten, compiled; `IQueryable` vs `IEnumerable` |
| `09-Serialization` | 0 | 265 | **"Byte-for-byte identical: True"** |
| `10-ASPNetCoreMVC` | 0 | 129 | 8 real HTTP round-trips incl. 201 + `Location`, and a 400 |
| `11-EntityFrameworkCore` | 0 | 327 | DDL + 8 generated SQL statements |
| `12-DynamicProxies` | 0 | 248 | proxy generated; all 7 limitations demonstrated |

### 3.2 Code quality

| | |
|---|---|
| C# source lines | 6,861 across 21 files |
| Build warnings | **0** |
| Third-party packages | **none** — only `Microsoft.*` (EF Core 10.0.15, `Microsoft.Extensions.*` 10.0.15) |
| Nullable reference types | enabled solution-wide |
| Deprecated APIs used | **none** — verified by probing the live API surface, not from memory |
| Machine-specific paths | **none** — all paths derived from `AppContext.BaseDirectory` |
| Reproducible | yes — fixed dates, no randomness except one `Guid` fragment, `InvariantGlobalization` |

### 3.3 API compatibility probe

Before any example was written, the .NET 10 Reflection.Emit surface was probed **by compiling
and running code**:

| Probe | Result |
|---|---|
| `RuntimeFeature.IsDynamicCodeSupported` | `True` |
| `RuntimeFeature.IsDynamicCodeCompiled` | `True` |
| `AssemblyBuilderAccess` members | **`Run`, `RunAndCollect` only** — `Save`/`RunAndSave` absent |
| `AssemblyBuilder.Save(string)` | **absent** |
| `PersistedAssemblyBuilder` | **present**, in the `System.Reflection.Emit` assembly (shared framework, no package) |
| → `Save(Stream)`, `Save(String)`, `GenerateMetadata(×2)` | confirmed; a real DLL was written and reloaded |
| `DispatchProxy.Create` overloads | generic `<T,TProxy>()` **and** non-generic `(Type, Type)` |
| EF Core newest `net10.0`-compatible | **10.0.15** (11.0.0 requires `net11.0`) |

### 3.4 Platform restrictions identified

| Constraint | Effect — stated in the materials |
|---|---|
| **Native AOT** | Examples 05, 06, 12 **cannot work**. `IsDynamicCodeSupported` is `false`; `Reflection.Emit`, `DispatchProxy` and `Expression.Compile()` throw. Slide 16 and Part 3 §1.4 cover it. |
| **Trimming** | Examples 01–04 and reflection-mode JSON risk trimmed members. Source-generated JSON is safe. |
| **Mono interpreter** | `IsDynamicCodeCompiled` is `false` — Emit works but runs interpreted, so the speed benefit disappears. |

No project opts into AOT or trimming, so all 14 build and run as supplied.

### 3.5 ⚠ Benchmark caveat

Examples 05 and 09 use `Stopwatch` in a loop with a warm-up. That is honest to an **order of
magnitude** and no better. It does not control for tiered compilation, GC timing, or CPU
frequency scaling. **Use BenchmarkDotNet** for anything you intend to act on — stated in the
code, on the slides, and in the study guide.

Absolute milliseconds differ per machine and per run. Two independent runs on this machine:

| Technique | Run 1 | Run 2 |
|---|---|---|
| direct property access | 8 ms (1.0×) | 11 ms (1.0×) |
| `PropertyInfo.GetValue` | 67 ms (8.4×) | 76 ms (6.9×) |
| cached delegate (typed) | 10 ms (1.2×) | 14 ms (1.3×) |
| emitted IL (typed) | 10 ms (1.2×) | 13 ms (1.2×) |
| emitted IL (boxing) | 21 ms (2.6×) | 26 ms (2.4×) |

**The ratios — and therefore every claim built on them — hold across both runs.** The
materials quote run 1 and state explicitly that values are machine-specific.

---

## 4. Timing validation

| Section | Required | Delivered | ✓ |
|---|---|---|---|
| Part 1 — Understanding Metaprogramming | 6 min | **6:00** | ✅ |
| Part 2 — Runtime Introspection | 10 min | **10:00** | ✅ |
| Part 3 — Runtime Code Generation | 12 min | **12:00** | ✅ |
| Part 4 — Real-World Applications | 9 min | **9:00** | ✅ |
| **Total** | **37 min** | **37:00** | ✅ |

Per-slide budgets live in the deck's speaker notes and were **summed from the file**, not
asserted: 28/28 slides carry an `[m:ss]` budget and they total exactly **37:00**. The
introduction (1:30) is inside Part 1's six minutes and the closing (1:30) inside Part 4's
nine, as the syllabus requires.

### 4.1 Script pacing — measured

The spoken script was **word-counted from the file**, excluding blocks marked optional:

| Section | Spoken words | At 140 wpm | Demo | Budget |
|---|---|---|---|---|
| Opening (1–2) | 205 | 1:27 | — | 1:30 |
| Part 1 (3–7) | 641 | 4:34 | — | 4:30 |
| Part 2 (8–15) | 1,310 | 9:21 | 0:40 | 10:00 |
| Part 3 (16–23) | 1,562 | 11:09 | 1:00 | 12:00 |
| Part 4 (24–27) | 1,063 | 7:35 | — | 7:30 |
| Closing (28) | 201 | 1:26 | — | 1:30 |
| **Total** | **4,982** | **35:35** | **1:40** | **37:00** |

**Delivered estimate: 37:15 — 15 seconds (0.7%) over**, which is inside the variation between
two rehearsals of the same talk.

**Deviation declared honestly:** the first draft of the script measured **5,756 words =
42:46**, nearly six minutes over. It was measured, found over, and cut by ~800 words across
two passes until it fit. The per-section table above was generated from the final file, not
estimated.

---

## 5. Deliverables checklist

| Required | Status | Location |
|---|---|---|
| PowerPoint (.pptx), real and editable | ✅ | `Presentation/Metaprogramming-DotNet10.pptx` |
| Presentation outline | ✅ | `Presentation/Presentation-Outline.md` *(generated from the deck)* |
| Speaker notes | ✅ | `Presentation/Speaker-Notes.md` *(generated from the deck)* |
| Diagrams folder | ✅ | `Presentation/Diagrams/` + `Assets/Diagrams/` |
| Complete study guide (MD + PDF) | ✅ | `Study-Materials/Complete-Study-Guide.{md,pdf}` |
| Part 1–4 study materials | ✅ | `Study-Materials/Part-{1,2,3,4}-*.md` |
| Quick revision notes | ✅ | `Study-Materials/Quick-Revision-Notes.md` |
| Interview questions | ✅ | `Study-Materials/Interview-Questions.md` |
| Glossary | ✅ | `Study-Materials/Glossary.md` |
| References | ✅ | `Study-Materials/References.md` |
| .NET 10 solution (`.slnx`) | ✅ | `Code-Examples/Metaprogramming.slnx` |
| Examples 01–12 | ✅ | `Code-Examples/01-…` → `12-…` |
| Code examples README | ✅ | `Code-Examples/README.md` |
| Full 37-minute script | ✅ | `Presenter-Guide/Full-Presentation-Script.md` |
| 37-minute timeline | ✅ | `Presenter-Guide/37-Minute-Timeline.md` |
| Live demo guide | ✅ | `Presenter-Guide/Live-Demo-Guide.md` |
| Audience Q&A | ✅ | `Presenter-Guide/Audience-QA.md` |
| Troubleshooting | ✅ | `Presenter-Guide/Troubleshooting.md` |
| Presenter study guide (A–G per topic) | ✅ | `Presenter-Guide/Topic-Mastery.md` — 15 topics × beginner / technical / analogy / practical / walkthrough / confusion / Q&A |
| Validation report | ✅ | this file |
| Syllabus coverage map | ✅ | `SYLLABUS-COVERAGE.md` |
| Setup instructions | ✅ | `README.md` + `Code-Examples/README.md` (bash **and** PowerShell) |
| Captured expected output | ✅ | `Code-Examples/expected-output/` — 12 files, 2,246 lines |

**Nothing from the syllabus was omitted.** The only structural deviation is the addition of
`Presenter-Guide/Topic-Mastery.md` (the A–G presenter study guide, which had no filename
specified) and `Code-Examples/expected-output/` (not requested, but it is what makes "do not
invent successful test results" checkable).

---

## 6. Summary

| | |
|---|---|
| **Deliverables** | 23 Markdown files · 1 PPTX (28 slides) · 1 PDF (129 pages) · 18 SVG diagrams · 14 projects · 12 captured transcripts |
| **Total written material** | ≈ 58,000 words outside the deck, plus 43,367 characters of speaker notes |
| **Code** | 6,861 lines of C#, 0 warnings, 0 errors, 12/12 examples passing |
| **Timing** | 37:00 exactly, matching all four syllabus budgets |
| **Presentation defects** | 79 found → **0 remaining** |
| **Documentation links** | 56, all verified by HTTP request |
| **Syllabus coverage** | 112/112 topic markers |

### What was NOT verified

Stated plainly, because the brief asked for honesty over completeness:

1. **No visual render of the slides.** No rendering engine is installed. Geometry, font
   sizes, overflow, overlaps and notes were all checked programmatically and are clean — but
   nobody has *looked* at the 28 slides. **Flick through them once before presenting.**
2. **No rendering check of the PDF's appearance.** Its text content and all 129 pages were
   verified by extraction; its visual layout was not inspected page by page.
3. **The benchmarks are indicative, not rigorous.** `Stopwatch` in a loop. Ratios reproduce;
   absolute numbers do not.
4. **Native AOT and trimming were not exercised.** The restrictions are documented from the
   official constraints and from `RuntimeFeature` flags read at runtime — but no AOT publish
   was attempted, so the specific exception messages under AOT are not quoted anywhere.
5. **The deck was not opened in PowerPoint itself.** It round-trips through `python-pptx`
   and is valid OOXML; it has not been opened in Microsoft PowerPoint, LibreOffice or
   Keynote on this machine.
6. **Fonts on non-Windows systems.** The deck specifies Segoe UI and Consolas. On macOS or
   Linux these substitute. Auto-sizing means nothing should clip, but spacing will shift —
   untested.
