# Metaprogramming in .NET 10

### Runtime Introspection, Reflection, and Dynamic Code Generation

A complete, presentation-ready learning package for a **37-minute** technical session:
slide deck, a 129-page study guide, twelve runnable .NET 10 examples, eighteen diagrams,
and a full presenter kit.

> **A .NET assembly is self-describing.** Metaprogramming reads that description, or writes
> new code from it. Every framework in Part 4 does it **once** at startup, **caches** the
> result, then runs **compiled code**.

---

## What's here

| | | |
|---|---|---|
| 🎤 | **[`Presentation/Metaprogramming-DotNet10.pptx`](Presentation/Metaprogramming-DotNet10.pptx)** | 28 slides, 16:9, dark conference theme, 16 editable diagrams, speaker notes on every slide |
| 📘 | **[`Study-Materials/Complete-Study-Guide.pdf`](Study-Materials/Complete-Study-Guide.pdf)** | 129 pages, 36,500 words |
| 💻 | **[`Code-Examples/`](Code-Examples/)** | 14 projects · **12 runnable examples** · all built and executed |
| 🗣 | **[`Presenter-Guide/`](Presenter-Guide/)** | script, timeline, demo guide, Q&A, topic mastery, troubleshooting |
| 📊 | **[`Assets/Diagrams/`](Assets/Diagrams/) ** | 18 SVG diagrams (vector, resolution-independent) |
| ✅ | **[`VALIDATION-REPORT.md`](VALIDATION-REPORT.md)** | what was verified, how, and what wasn't |

**Everything quoted in the slides and the study guide is real output**, captured by running
the code — not written from expectation. The transcripts are in
[`Code-Examples/expected-output/`](Code-Examples/expected-output/) so you can diff your own
run.

---

## Quick start

```bash
# 1. Build and run the code  (needs the .NET 10 SDK)
cd Code-Examples
dotnet restore Metaprogramming.slnx
dotnet build Metaprogramming.slnx -c Release

# 2. The two best examples
dotnet run -c Release --project 05-DynamicMethod          # IL generation + measured benchmark
dotnet run -c Release --project 04-Plugin-Discovery/PluginDiscovery.Host
```

Then open `Presentation/Metaprogramming-DotNet10.pptx`, or read
`Study-Materials/Complete-Study-Guide.pdf`.

**Presenting this?** Start with
[`Presenter-Guide/37-Minute-Timeline.md`](Presenter-Guide/37-Minute-Timeline.md), then
[`Presenter-Guide/Full-Presentation-Script.md`](Presenter-Guide/Full-Presentation-Script.md).

---

## The session

| Part | Topic | Time | Slides |
|---|---|---|---|
| **1** | Understanding metaprogramming — normal vs meta, the three stages, implicit vs explicit metadata | 6 min | 3–7 |
| **2** | Runtime introspection — metadata, the Reflection API, `BindingFlags`, `Activator`, attributes, plugin discovery | 10 min | 8–15 |
| **3** | Runtime code generation — CIL, the evaluation stack, `DynamicMethod`, `TypeBuilder`, persistence | 12 min | 16–23 |
| **4** | Real-world applications — MVC, DI, EF Core, serialization, proxies, expression trees | 9 min | 24–27 |
| | **Total** | **37 min** | **28** |

The opening and closing are absorbed into Parts 1 and 4. Per-slide budgets are in the
timeline and in the deck's speaker notes; they sum to exactly 37:00.

---

## The code

Fourteen projects — twelve runnable examples plus two support libraries. Run them in order;
each builds on the last.

| # | Example | The one thing to take away |
|---|---|---|
| 01 | `01-Basics` | One reflective routine replaces N hand-written ones — and gives up compile-time safety to do it |
| 02 | `02-Reflection` | Every `Activator` failure is a **runtime** failure |
| 03 | `03-Attributes` | Reading an attribute **constructs** it; `GetCustomAttributesData` does not |
| 04 | `04-Plugin-Discovery` | `AssemblyLoadContext` isolates **type identity**, not **privileges** |
| 05 | `05-DynamicMethod` | **A cached delegate is as fast as emitted IL** — measured |
| 06 | `06-ReflectionEmit` | Generate a type implementing a **known interface**, then never reflect again |
| 07 | `07-DependencyInjection` | A constructor signature **is** a machine-readable dependency list |
| 08 | `08-ExpressionTrees` | "Cannot be *expressed* as a tree" ≠ "cannot be *translated* to SQL" |
| 09 | `09-Serialization` | Same output, two mechanisms — the difference is *when* the work happened |
| 10 | `10-ASPNetCoreMVC` | Reflection at **startup**; requests served from a cached table |
| 11 | `11-EntityFrameworkCore` | The EF model is a **separate, richer** model built by reading CLR metadata |
| 12 | `12-DynamicProxies` | Metaprogramming gives you the **seam**, not the **policy** |

Full details, per-example commands, and PowerShell notes:
[`Code-Examples/README.md`](Code-Examples/README.md).

---

## Verified on this machine

Checked by **running code**, not recalled from documentation. These are the points most
likely to be wrong in older material.

| Finding | Where |
|---|---|
| **`AssemblyBuilderAccess` on .NET 10 has only `Run` and `RunAndCollect`.** `Save` and `RunAndSave` were .NET *Framework* and do not exist — code using them does not compile. | example 06 §1 |
| **Saving a generated assembly *is* supported** — via `PersistedAssemblyBuilder`. Wrote a 2 KB DLL, reloaded it with `Assembly.LoadFrom`, invoked it. | example 06 §6 |
| **Invalid IL is not always caught.** A missing `ret` threw `InvalidProgramException` on first call. But `add` on an `int` and a `string` threw **nothing** and silently returned garbage. | example 05 §8 |
| **A cached delegate measured identical to emitted IL** (both ≈1.2× direct). Try `MethodInfo.CreateDelegate` before writing any IL. | example 05 §7 |
| **Boxing dominates.** The same emitted getter cost 1.2× or 2.4× purely by signature shape — more than the reflection-vs-emit choice. | example 05 §7 |
| **The DI engine is chosen at runtime** (`DynamicServiceProviderEngine` here), falling back to reflection-only under AOT. So *"DI uses reflection"* — not *"DI uses Reflection.Emit"*. | example 07 §8 |
| **`JsonSerializerOptions`: varying the settings is the expensive mistake**, not allocating a new instance (1.8× vs 9.2×). Equivalent instances share a process-wide cache. | example 09 §3 |
| **A `DispatchProxy` base class must not be `sealed`.** .NET 10 also has a non-generic `DispatchProxy.Create(Type, Type)`. | example 12 §8 |
| **SQLite renders `string.Contains` as `instr(...) > 0`, not `LIKE`.** The *provider* chooses — read `ToQueryString()`. | example 11 §5B |
| **A collectible `AssemblyLoadContext` really does unload** — on the second GC attempt, with every reference dropped. | example 04 §4 |

---

## Requirements

| | |
|---|---|
| **.NET SDK** | 10.0 or later (built and verified on **10.0.400**, runtime **10.0.11**) |
| **Database** | none — the EF Core example uses SQLite **in-memory** |
| **IDE** | none required; the `dotnet` CLI is enough |
| **To open the deck** | PowerPoint, LibreOffice Impress, Google Slides or Keynote |

Everything targets `net10.0`. Only three projects need NuGet packages (EF Core 10.0.15 and
`Microsoft.Extensions.*` 10.0.15); the rest use only the shared framework.

### Platform limits worth knowing

| Constraint | Effect |
|---|---|
| **Native AOT** | Examples 05, 06 and 12 **cannot work** — `RuntimeFeature.IsDynamicCodeSupported` is `false` and `Reflection.Emit` / `DispatchProxy` throw. This is itself the subject of slide 16. |
| **Trimming** | Examples 01–04 and reflection-mode JSON risk having members trimmed. Source-generated JSON (09) is trimming-safe. |
| **Mono interpreter** (iOS) | Emit *works* but runs interpreted — the speed benefit disappears. |

No project opts into AOT or trimming, so all 14 build and run as supplied.

---

## Repository layout

```text
Metaprogramming-DotNet10/
├── README.md                     ← you are here
├── VALIDATION-REPORT.md          what was verified, and what wasn't
├── SYLLABUS-COVERAGE.md          every topic → slide, chapter, example
├── PROGRESS.md                   build log / resume point
│
├── Presentation/
│   ├── Metaprogramming-DotNet10.pptx    28 slides, notes on every one
│   ├── Presentation-Outline.md          generated FROM the deck
│   ├── Speaker-Notes.md                 all 28 notes, 43k characters
│   └── Diagrams/                        the 18 SVGs (copies)
│
├── Study-Materials/
│   ├── Complete-Study-Guide.md / .pdf   129 pages, 36,500 words
│   ├── Part-1-Metaprogramming.md        6-minute section, in full
│   ├── Part-2-Runtime-Introspection.md  10-minute section
│   ├── Part-3-Runtime-Code-Generation.md 12-minute section
│   ├── Part-4-Real-World-Applications.md 9-minute section
│   ├── Quick-Revision-Notes.md          for 15 minutes before you present
│   ├── Interview-Questions.md           22 questions, accurate answers
│   ├── Glossary.md                      every term, with misuses flagged
│   └── References.md                    Microsoft docs — all links verified
│
├── Code-Examples/
│   ├── Metaprogramming.slnx             14 projects
│   ├── 01-Basics … 12-DynamicProxies
│   └── expected-output/                 captured output of all 12
│
├── Presenter-Guide/
│   ├── Full-Presentation-Script.md      word-for-word, measured at 37:15
│   ├── 37-Minute-Timeline.md            cumulative clock + cut list
│   ├── Live-Demo-Guide.md               2 essential demos, 2 optional
│   ├── Topic-Mastery.md                 15 topics × beginner/technical/analogy/Q&A
│   ├── Audience-QA.md                   the 14 hardest questions, answered aloud
│   └── Troubleshooting.md               everything that can go wrong
│
└── Assets/Diagrams/                     18 SVGs + index
```

---

## Where to start, by who you are

**Presenting it.**
[`37-Minute-Timeline.md`](Presenter-Guide/37-Minute-Timeline.md) →
[`Full-Presentation-Script.md`](Presenter-Guide/Full-Presentation-Script.md) →
[`Live-Demo-Guide.md`](Presenter-Guide/Live-Demo-Guide.md). Rehearse twice with a timer.
Skim [`Topic-Mastery.md`](Presenter-Guide/Topic-Mastery.md) for the analogies and the
"common confusion" sections.

**Learning it.** Read the four Parts in order, running the matching example alongside each.
Start with `01-Basics` and `05-DynamicMethod`.

**Attended the session.** [`Quick-Revision-Notes.md`](Study-Materials/Quick-Revision-Notes.md)
is the one-page recap. Then run `05-DynamicMethod` in Release — it makes the performance
argument for you.

**Interviewing.** [`Interview-Questions.md`](Study-Materials/Interview-Questions.md), then
[`Glossary.md`](Study-Materials/Glossary.md).

**Just want the useful bit.** Example 05's benchmark, and this: *try
`MethodInfo.CreateDelegate` before you ever write IL.*

---

## The decision order

The practical takeaway of the whole session. Work down the list; stop at the first thing
that works.

```text
1. plain C# / generics / a cached delegate      ← covers most cases
2. expression trees                             ← if you need a FUNCTION
3. DispatchProxy                                ← if you only need to intercept an INTERFACE
4. a Roslyn source generator                    ← if the shape is known at BUILD time
5. Reflection.Emit                              ← only for a TYPE, known only at RUN time
```

Most people who reach for step 5 needed step 1 or 2.

---

## Three claims to get right

These are the ones most often repeated wrongly:

1. **`AssemblyLoadContext` is not a security sandbox.** It isolates type identity and
   versions, not privileges. There is no supported in-process sandbox in modern .NET.
2. **`AssemblyBuilderAccess.Save` does not exist.** Use `PersistedAssemblyBuilder`.
3. **"DI uses reflection"** — not "DI uses Reflection.Emit". The activation strategy depends
   on the container, its version and the platform.

---

## Honest limitations

See [`VALIDATION-REPORT.md`](VALIDATION-REPORT.md) for the full list. The main ones:

- **The deck was not visually rendered.** LibreOffice is not installed on the build machine,
  so slides could not be rasterised and eyeballed. Instead the `.pptx` was inspected
  programmatically — shape bounds, font sizes, estimated text overflow, overlaps, notes
  presence and timing. **0 issues remain.** A human should still flick through it once.
- **The benchmarks use `Stopwatch` in a loop.** Honest to an order of magnitude, no better.
  Use [BenchmarkDotNet](https://benchmarkdotnet.org/) for anything you act on.
- **Timings differ per machine.** Treat the ratios as the lesson.
- **The PDF was generated with ReportLab**, not a Markdown toolchain (no pandoc available),
  so it renders a deliberate subset of Markdown. All 129 pages and every section were
  verified present by extracting the text back out.

---

## Licence and attribution

Session material prepared for a technical knowledge-sharing session. The code examples are
intended to be copied, run and adapted freely.

All technical claims are referenced to official Microsoft documentation in
[`Study-Materials/References.md`](Study-Materials/References.md) — every URL checked with an
HTTP request at build time.
