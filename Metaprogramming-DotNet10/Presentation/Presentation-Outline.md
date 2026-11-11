# Presentation Outline

Auto-generated from `Metaprogramming-DotNet10.pptx` so it cannot drift from the deck.

| | |
|---|---|
| Slides | 28 |
| Aspect ratio | 16:9 (13.333 × 7.5 in) |
| Total runtime | **37:00** |
| Diagrams embedded | 16, as native editable PowerPoint shapes |
| Speaker notes | on all 28 slides |

---

## Opening

*1 min 30 s · slides 1–2*

### Slide 1 — Metaprogramming in .NET 10

**0:45** · Opening

> **Intent:** TITLE — set the frame, then move quickly.

*TRANSITION: "Here's where we're going." -> Slide 2 (agenda).*

---

### Slide 2 — Where we are going — and what you leave with

**0:45** · Opening

*Visuals:* 1 table

> **Intent:** AGENDA — say the shape, not the contents.

*TRANSITION: "So let's start with the obvious question: what IS metaprogramming?"*

---

## Part 1 — Understanding Metaprogramming

*4 min 30 s of slide time (6 min with the opening) · slides 3–7*

### Slide 3 — What is metaprogramming?

**1:00** · Part 1

*Visuals:* 1 code/output block

- Normal programming — your code operates on DATA
- Metaprogramming — your code operates on the PROGRAM
  - "Meta-" simply means "about". Programming about programs.
- Two halves, and they are Parts 2 and 3 of this session:
  - INTROSPECTION — reading the program's own structure
  - CODE GENERATION — creating new executable code

> **Intent:** DEFINITION — get the mental model in place; everything else hangs off it.

*TRANSITION: "Let me make the difference concrete." -> Slide 4 (diagram).*

---

### Slide 4 — Normal programming vs metaprogramming

**1:00** · Part 1

*Visuals:* diagram `01-normal-vs-metaprogramming`

> **Intent:** DIAGRAM — the fork in the road.

*TRANSITION: "So why would anyone accept that trade? Because sometimes the left-hand*

---

### Slide 5 — Why do we need it? Start with the problem

**0:50** · Part 1

*Visuals:* 2 code/output blocks

  - 40 methods of pure mechanism
  - Add Product.Weight -> the audit is silently wrong
  - A plugin type? You CANNOT write its method
  - Works for all 40, and every future type
  - Cannot go stale when a property is added
  - Handles types from a DLL loaded at run time

> **Intent:** PROBLEM FIRST — this is the slide that earns the audience's trust.

*TRANSITION: "So what do we actually get, and when does it happen?" -> Slide 6.*

---

### Slide 6 — Benefits — and the three stages that get confused

**1:00** · Part 1

*Visuals:* diagram `02-compiletime-vs-runtime`

  - AUTOMATION — derive mechanism from declarations
  - LESS BOILERPLATE — one attribute replaces a mapping layer
  - MAINTAINABILITY — fix the mechanism in one place
  - EXTENSIBILITY — the only one you cannot get another way
  - The type set is closed -> use generics
  - Called once -> generation costs more than it saves
  - The team will not maintain it
  - You are shipping Native AOT -> much of it will not run

> **Intent:** BENEFITS + THE THREE STAGES. Two things on one slide; the diagram leads.

*TRANSITION: "One last idea before we open the API: where does metadata come from?"*

---

### Slide 7 — Implicit vs explicit metadata

**0:40** · Part 1

*Visuals:* 2 code/output blocks

> **Intent:** IMPLICIT vs EXPLICIT — short slide, one precision point. Keep it tight.

*TRANSITION: "That's Part 1. Now let's actually read this metadata." -> Slide 8.*

---

## Part 2 — Runtime Introspection

*10 min · slides 8–15*

### Slide 8 — What is runtime introspection?

**0:50** · Part 2

*Visuals:* 1 code/output block

- A program examining its OWN structure while it runs
  - It reads metadata — not your source files
  - Source is not deployed and is not needed
- Questions it can answer:
  - What types are in this DLL?
  - What methods does this type have?
  - Is this method marked [Obsolete]?

> **Intent:** INTROSPECTION — orient, then move. Part 2 is dense; do not linger here.

*TRANSITION: "To use the API well you need the shape of the metadata. It's a hierarchy."*

---

### Slide 9 — Understanding .NET metadata

**1:20** · Part 2

*Visuals:* diagram `03-assembly-metadata`

> **Intent:** METADATA STRUCTURE — the map for the rest of Part 2. Worth the time.

*TRANSITION: "Here's the API that reads it." -> Slide 10.*

---

### Slide 10 — The Reflection API

**1:10** · Part 2

*Visuals:* diagram `04-reflection-workflow`, 1 table

> **Intent:** THE API — five steps and six types. Point, don't read.

*TRANSITION: "Let's start at the top of that graph — finding a type." -> Slide 11.*

---

### Slide 11 — Assembly and type discovery

**1:20** · Part 2

*Visuals:* diagram `05-assembly-types-members`, 1 code/output block

  - or you get a silent null.

> **Intent:** TYPE DISCOVERY — one diagram, four APIs, one famous trap.

*TRANSITION: "Once you have a type, you look inside it." -> Slide 12.*

---

### Slide 12 — Inspecting members — and BindingFlags

**1:20** · Part 2

*Visuals:* 2 code/output blocks, 1 table

> **Intent:** MEMBERS + BindingFlags — two takeaways, both practical.

*TRANSITION: "Reading is half of it. Now let's CREATE an object from a Type." -> Slide 13.*

---

### Slide 13 — Dynamic object creation — Activator.CreateInstance

**1:20** · Part 2

*Visuals:* diagram `06-activator-workflow`

> **Intent:** ACTIVATOR — the bridge from "a Type I discovered" to "a live object".

*TRANSITION: "One more ingredient before we build something real: attributes." -> Slide 14.*

---

### Slide 14 — Attributes and custom attributes

**1:30** · Part 2

*Visuals:* diagram `07-attribute-inspection`, 2 code/output blocks, 1 table

> **Intent:** ATTRIBUTES — the richest slide in Part 2. Three points; budget your time.

*TRANSITION: "Interface, plus attribute, plus reflection, plus Activator. Put those four*

---

### Slide 15 — Practical example — dynamic plugin discovery

**1:10** · Part 2

*Visuals:* diagram `08-plugin-discovery`

> **Intent:** PLUGIN DISCOVERY — the payoff slide for Part 2. Everything so far, combined.

*TRANSITION: "That's reading metadata. Now — can we WRITE code at run time?" -> Slide 16.*

---

## Part 3 — Runtime Code Generation

*12 min · slides 16–23*

### Slide 16 — What is runtime code generation?

**1:00** · Part 3

*Visuals:* 1 code/output block, 1 table

- Creating NEW executable code while the process runs
  - Part 2 READ metadata that existed. Part 3 WRITES code that did not.
- There is exactly one good reason:
  - Pay the reflection cost ONCE, then call generated code
  - Convert a per-call cost into a one-off cost
- And one structural reason:
  - You need a TYPE that did not exist — a mock, a proxy

> **Intent:** OPENING PART 3 — frame it as the mirror of Part 2, and be honest about motive.

*TRANSITION: "Before the IL, let's be precise about how these two APIs differ."*

---

### Slide 17 — Reflection vs Reflection.Emit

**1:20** · Part 3

*Visuals:* diagram `09-reflection-vs-emit`, 1 table

> **Intent:** REFLECTION vs EMIT — the comparison the syllabus asks for, plus the measured

*TRANSITION: "To emit IL you need to know a little IL. Let's build that up." -> Slide 18.*

---

### Slide 18 — Introduction to CIL / MSIL

**1:30** · Part 3

*Visuals:* diagram `10-compilation-pipeline`

  - CIL = MSIL = IL — three names, same thing (ECMA-335)
  - Stack-based, CPU-independent, and fully typed
  - One assembly runs on x64, Arm64, Windows, Linux, macOS

> **Intent:** THE PIPELINE — the conceptual foundation for the next five slides.

*TRANSITION: "So what does IL actually look like? It's a stack machine." -> Slide 19.*

---

### Slide 19 — IL instructions and the evaluation stack

**2:00** · Part 3

*Visuals:* diagram `11-evaluation-stack`

> **Intent:** THE EVALUATION STACK — the longest slide in the deck, and worth every second.

*TRANSITION: "Now let's generate one of these for real." -> Slide 20.*

---

### Slide 20 — DynamicMethod, ILGenerator and CreateDelegate

**1:40** · Part 3

*Visuals:* diagram `12-dynamicmethod-pipeline`

> **Intent:** THE PIPELINE — six steps, two warnings.

*TRANSITION: "Let's see it work." -> Slide 21 (demo).*

---

### Slide 21 — Practical example — generating a method at runtime

**2:00** · Part 3

*Visuals:* 2 code/output blocks, 1 table

> **Intent:** DEMO SLIDE (essential demo #2). Budget: ~50s talking, ~60s demo.

*TRANSITION: "A single method is useful. Sometimes you need a whole TYPE." -> Slide 22.*

---

### Slide 22 — AssemblyBuilder, ModuleBuilder, TypeBuilder, MethodBuilder

**1:30** · Part 3

*Visuals:* diagram `13-runtime-type-generation`

> **Intent:** THE BUILDERS — the hierarchy, the design pattern, and one important correction.

*TRANSITION: "Let's generate a type and then save it to disk." -> Slide 23.*

---

### Slide 23 — Generating types and methods at runtime

**1:00** · Part 3

*Visuals:* 2 code/output blocks

> **Intent:** GENERATED TYPE, END TO END. Tight slide — the heavy lifting was slide 22.

*TRANSITION: "You now have everything you need to recognise this machinery in the*

---

## Part 4 — Real-World Applications

*7 min 30 s of slide time (9 min with the closing) · slides 24–27*

### Slide 24 — ASP.NET Core MVC and dependency injection

**2:15** · Part 4

*Visuals:* diagram `18-mvc-di-combined`

> **Intent:** MVC + DI — the biggest slide in Part 4. Two topics, one lesson.

*TRANSITION: "Same pattern, two more frameworks." -> Slide 25.*

---

### Slide 25 — Entity Framework Core and serialization

**2:00** · Part 4

*Visuals:* 3 code/output blocks, 1 table

  - DbSet<T> discovery · conventions < annotations < fluent API
  - The EF model is a SEPARATE, richer model built FROM CLR metadata
  - Shadow properties exist in the model with NO CLR property

> **Intent:** EF CORE + SERIALIZATION. Two topics; the EF half is the more impressive demo.

*TRANSITION: "Two more places this shows up." -> Slide 26.*

---

### Slide 26 — Plugin architectures and dynamic proxies

**1:30** · Part 4

*Visuals:* diagram `17-dynamic-proxy`

- PLUGINS
  - ALC isolates TYPE IDENTITY, not privileges
  - Unload() is a REQUEST — one cached Type blocks it
- PROXIES
  - Interfaces only; the base proxy must not be sealed
  - SELF-CALLS are not intercepted; async needs care

> **Intent:** PLUGINS + PROXIES. Plugins are a recap; proxies are new. Weight it ~25/65.

*TRANSITION: "Which brings us to the last technique — and the one I'd actually recommend."*

---

### Slide 27 — Cross-cutting concerns and expression trees

**1:45** · Part 4

*Visuals:* diagram `16-expression-tree`, 1 code/output block, 1 table

  - Compile() → run in memory.  Give it to EF Core → SQL.
  - Type-checked as you build. Expression.Add THROWS on a mismatch

> **Intent:** EXPRESSION TREES + CHOOSING A SEAM. End Part 4 on the recommendation.

*TRANSITION: "Let me leave you with five things." -> Slide 28.*

---

## Closing

*1 min 30 s · slide 28*

### Slide 28 — Questions

**1:30** · Closing

- A .NET assembly is SELF-DESCRIBING — that is why all of this works
- DISCOVER ONCE · CACHE · THEN EXECUTE COMPILED CODE
  - Every framework in Part 4 does exactly this
- Try MethodInfo.CreateDelegate before you write any IL
  - Measured: identical speed to emitted IL, none of the risk
- Expression trees for a FUNCTION. Reflection.Emit only for a TYPE
- Metaprogramming trades COMPILE-TIME SAFETY for RUN-TIME FLEXIBILITY
  - Make that trade when you need the flexibility — not one step sooner
- AssemblyLoadContext is NOT a security sandbox
- AssemblyBuilderAccess.Save does not exist — use PersistedAssemblyBuilder
- "DI uses reflection" — not "DI uses Reflection.Emit"

> **Intent:** CLOSING — five takeaways, then open the floor. Do not add new material here.

---
