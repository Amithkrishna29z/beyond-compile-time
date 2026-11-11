# Diagrams

18 diagrams, as **SVG** — vector, so they are resolution-independent and stay sharp at
any projector or print size.

The same 18 files are copied into
[`../../Presentation/Diagrams/`](../../Presentation/Diagrams/) for convenience.

## How these relate to the deck

Each diagram is declared **once** as data (nodes, edges, styles) and rendered two ways:

1. **Into the .pptx as native PowerPoint shapes** — rounded rectangles and connectors
   with real arrowheads. They are fully **editable**: click any box and retype it, drag
   it, recolour it. Nothing in the deck is a flattened image.
2. **Into these SVG files** — for the study guide, the repo README, and any other use.

Because both come from one source, the deck and these assets cannot disagree.

## The 17 required diagrams

| File | Deck slide | Shows |
|---|---|---|
| `01-normal-vs-metaprogramming.svg` | 4 | reading a VALUE vs reading the STRUCTURE |
| `02-compiletime-vs-runtime.svg` | 6 | the three stages: build-time generation, runtime introspection, runtime generation |
| `03-assembly-metadata.svg` | 9 | Assembly → Manifest / Module → Type → Members, and where attributes attach |
| `04-reflection-workflow.svg` | 10 | the five-step Reflection workflow, and where to cache |
| `05-assembly-types-members.svg` | 11 | the fan-out from Assembly to Types to Members |
| `06-activator-workflow.svg` | 13 | `Activator.CreateInstance` validation → binding → construction, with every failure mode |
| `07-attribute-inspection.svg` | 14 | how an applied attribute becomes a metadata row, and the two ways to read it back |
| `08-plugin-discovery.svg` | 15 | host / contracts / plugin folder, the load context, and the sandbox warning |
| `09-reflection-vs-emit.svg` | 17 | work-repeats vs work-happens-once, with the measured ratios |
| `10-compilation-pipeline.svg` | 18 | C# → IL + metadata → JIT → machine code, plus AOT / R2R / interpreter |
| `11-evaluation-stack.svg` | 19 | the stack after each of `ldarg.0`, `ldarg.1`, `add`, `ret` |
| `12-dynamicmethod-pipeline.svg` | 20 | `DynamicMethod` → `ILGenerator` → `Emit` → `CreateDelegate` → JIT → cached delegate |
| `13-runtime-type-generation.svg` | 22 | the builder hierarchy, `CreateType()`, and the cast-to-interface pattern |
| `14-di-resolution.svg` | — (see note) | resolution: find registration → read ctor params → resolve recursively → construct |
| `15-mvc-action-discovery.svg` | — (see note) | MVC startup discovery vs per-request serving |
| `16-expression-tree.svg` | 27 | the node graph for `c => c.Age > 18 && c.Country == "IN"` |
| `17-dynamic-proxy.svg` | 26 | caller → generated proxy → `Invoke` → real service, with before/after advice |

### Plus one composite

| File | Deck slide | Why it exists |
|---|---|---|
| `18-mvc-di-combined.svg` | 24 | Slide 24's title covers **both** MVC discovery and DI. Diagrams 14 and 15 are each ~450 units tall; stacking them on one 16:9 slide would push the labels below readable size. This composite carries the single lesson both of them teach — *reflection at startup, cached delegates per request* — at full legibility. |

So **14** and **15** are complete standalone assets (and are referenced from
`Study-Materials/Part-4-Real-World-Applications.md` §1 and §2); the deck shows the
composite in their place.

## Readability

Both renderers were checked programmatically:

- **no text overflows its box** in any of the 18 SVGs (estimated glyph widths vs box
  width, every line of every label);
- **no two content boxes overlap** (container frames excluded, since those are
  deliberately nested);
- in the .pptx, **no diagram label renders below 9 pt** — the slide layouts were adjusted
  until that held, which is why a few diagrams use a deliberately wide, short canvas.

## Palette

Dark navy ground with blue / cyan / purple accents, matching the deck.

| Role | Hex |
|---|---|
| Background | `#0E1628` |
| Panel | `#18233D` / `#1F2C4A` |
| Blue (primary) | `#4C8DFF` |
| Cyan (accent) | `#2CD8E8` |
| Purple (generation) | `#A98BFF` |
| Green (good / fast) | `#3FD69B` |
| Amber (caution) | `#FFC24B` |
| Red (danger / slow) | `#FF7B7B` |
| Text | `#E9EEF9` on dark |

Colour is used consistently: **purple** marks code generation, **green** marks the fast
or correct path, **red** marks a genuine hazard (unverified IL, the ALC sandbox myth),
**amber** marks a caution.

## Editing

To change a diagram, edit the **deck** copy (the shapes are editable) or re-render from
the spec. The generator scripts live outside this package, in the session's scratch
directory; the SVGs here are the durable artifact.
