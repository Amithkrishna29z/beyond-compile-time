# Progress Tracker

**Status: COMPLETE.** All six phases finished and validated on 2026-11-11.
See [`VALIDATION-REPORT.md`](VALIDATION-REPORT.md) for the evidence.

This file is kept as the build log and as a resume point if the package is ever extended.

---

## Phase 0 — Environment inspection (DONE 2026-11-10)

Verified by running commands on this machine:

| Item | Result |
|---|---|
| .NET SDKs installed | `8.0.427`, `10.0.400` |
| .NET runtimes | `Microsoft.NETCore.App 10.0.11`, `Microsoft.AspNetCore.App 10.0.11` |
| Target framework used | `net10.0` |
| OS | Windows 11 (10.0.26200), win-x64 |
| Python | 3.12.5 |
| python-pptx | 1.0.2 — used to build the .pptx |
| reportlab | 5.0.0 — used to build the study-guide PDF |
| pypdf | installed during the session — used to verify the PDF |
| markdown (python) | 3.11.3 — installed during the session |
| pandoc | **NOT installed** |
| LibreOffice / soffice | **NOT installed** → slides cannot be rendered to images |
| wkhtmltopdf / weasyprint | **NOT installed** |
| NuGet connectivity | working (api.nuget.org reachable) |
| EF Core latest `net10.0`-compatible | **`10.0.15`** (11.0.0 requires net11.0) |

## Phase 0b — Reflection.Emit API probe on .NET 10.0.11 (DONE)

Probed by compiling and running real code, **not** from memory:

- `RuntimeFeature.IsDynamicCodeSupported` = **True**, `IsDynamicCodeCompiled` = **True**.
- `AssemblyBuilderAccess` members on .NET 10 = **`Run`, `RunAndCollect` only**.
  `Save` and `RunAndSave` (.NET Framework) **do not exist**.
- `AssemblyBuilder.Save(string)` — **absent** on the runtime builder.
- `AssemblyBuilder` is declared in `System.Private.CoreLib`.
- `System.Reflection.Emit.PersistedAssemblyBuilder` — **exists**, in the
  `System.Reflection.Emit` assembly (shared framework, no package reference needed).
  Overloads confirmed: `Save(Stream)`, `Save(String)`, `GenerateMetadata(×2)`.
  Verified end to end: emitted a type, `Save`d a 2,048-byte DLL, `Assembly.LoadFrom`ed it
  and invoked the generated method (returned 42).
- `DynamicMethod` + `ILGenerator` + `CreateDelegate<T>()` verified.
- `DispatchProxy` present, with **both** `Create<T,TProxy>()` and `Create(Type, Type)`.

---

## Phases

- [x] **Phase 1 — Planning.** Syllabus mapped to slides/chapters/examples
      (`SYLLABUS-COVERAGE.md`); folder structure created; tooling probed.
- [x] **Phase 2 — Runnable code.** Built **first**, so every quoted output is real.
      14 projects, Release build clean (0 warnings), **12/12 examples pass**, output
      captured to `Code-Examples/expected-output/`.
- [x] **Phase 3 — Study materials.** 4 Parts + revision + interview + glossary +
      references; combined guide (36,539 words) and PDF (129 pages).
- [x] **Phase 4 — Presentation.** 28 slides, 16:9, dark theme; 18 diagrams declared once
      as data and rendered both as native PowerPoint shapes **and** as SVG; 79 layout
      defects found and fixed to **0**.
- [x] **Phase 5 — Presenter resources.** Script (measured at 37:15), timeline, demo guide,
      topic-mastery guide, audience Q&A, troubleshooting.
- [x] **Phase 6 — Final verification.** Clean rebuild, all examples re-run, links checked,
      counts corrected, `VALIDATION-REPORT.md` written.

---

## Things that were measured, found wrong, and corrected

Kept here because they are the most useful record in this file.

| Claim as first written | What measurement showed | Action |
|---|---|---|
| "Invalid IL throws `InvalidProgramException`" | A missing `ret` does. `add` on int+string threw **nothing** and returned garbage. | Rewrote the section around the asymmetry — a stronger lesson. |
| "A fresh `JsonSerializerOptions` re-reflects every call" | Only 1.4–1.8×; a process-wide cache is shared between *equivalent* instances. **Varying** the settings costs 7–9×. | Re-measured three ways; corrected the advice. |
| "SQLite translates `string.Contains` to `LIKE`" | It emits `instr(...) > 0`. | Corrected, and made provider-specificity a teaching point. |
| "13 projects" | The solution has **14** (12 runnable + 2 libraries). | Corrected across README, deck, script, demo guide, study guide. |
| Benchmark comparing a boxing emitted getter vs a non-boxing cached delegate | Unfair comparison. | Added a typed emitted variant — which revealed that **boxing dominates**, now a headline finding. |
| Script "≈4,950 words, 37:00" | Measured **5,756 words = 42:46**. | Cut ~800 words over two passes to land at 37:15, and printed the measured table. |

---

## If this package is extended

- **Diagrams** are declared once as data and rendered to both PPTX shapes and SVG. The
  generator scripts live in this session's scratch directory, not in the package; the SVGs
  and the deck are the durable artifacts. To change a diagram, edit the shapes in the deck
  (they are editable) and the SVG in `Assets/Diagrams/`.
- **`Presentation-Outline.md` and `Speaker-Notes.md` are generated FROM the .pptx.** Edit
  the deck, then regenerate — do not edit them by hand.
- **`Complete-Study-Guide.md` is assembled from the four Parts** plus revision, interview,
  glossary and references. Edit the Parts, then rebuild the combined guide and the PDF.
- **Re-run the examples after any code change** and re-capture
  `Code-Examples/expected-output/`, because the study materials quote those transcripts.

## Known gaps (also in VALIDATION-REPORT.md §6)

1. No visual render of the slides — no rendering engine installed. Geometry is clean;
   **a human should still look at all 28 slides once.**
2. The PDF's text and page count are verified; its page-by-page appearance is not.
3. Benchmarks are `Stopwatch`-in-a-loop: ratios reproduce, absolute numbers do not.
4. Native AOT / trimming restrictions are documented from `RuntimeFeature` flags and
   official constraints, but no AOT publish was attempted.
5. The deck has not been opened in PowerPoint, LibreOffice or Keynote on this machine.
6. Font substitution on macOS/Linux is untested.
