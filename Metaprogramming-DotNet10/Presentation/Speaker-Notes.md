# Speaker Notes

The complete notes field of every slide, exported from
`Metaprogramming-DotNet10.pptx`. These are the same notes PowerPoint shows in
Presenter View — this file exists so they can be read, printed and reviewed
without opening the deck.

Each block starts with `[m:ss]`, the slide's time budget. The budgets sum to **37:00**.

For a word-for-word spoken version, see
[`../Presenter-Guide/Full-Presentation-Script.md`](../Presenter-Guide/Full-Presentation-Script.md).

---

## Slide 1 — Metaprogramming in .NET 10

**0:45** · Opening

```text
[0:45]  TITLE — set the frame, then move quickly.

Say:
"Most of us use metaprogramming every day without calling it that. If you've written
an ASP.NET Core controller, used Entity Framework, serialized JSON, or injected a
dependency — you've relied on code that inspects your types and sometimes writes new
code at runtime. Today we're going to look inside that machinery."

Make one promise and one commitment:
  • Promise: by the end you'll know when to reach for this, and — more importantly —
    when NOT to.
  • Commitment: every number and every console output in this deck came from actually
    running the code on .NET 10.0.11. Nothing is quoted from memory.

Housekeeping (10 seconds, no more): all code, a 129-page study guide and the diagrams
are in the repo; mention you'll share the link at the end.

TRANSITION: "Here's where we're going." -> Slide 2 (agenda).
```

## Slide 2 — Where we are going — and what you leave with

**0:45** · Opening

```text
[0:45]  AGENDA — say the shape, not the contents.

Point at the four parts, read only the "what you will be able to do" column. Do not
read the table aloud in full; the audience can read faster than you can speak.

Emphasise the time column: "Part 3 is the biggest block — twelve minutes on IL and code
generation. That's deliberate. It's the part people find most mysterious and it's where
most of the bad advice on the internet lives."

Read the blue box ALOUD, word for word. It is the thesis of the whole talk and you will
come back to it on slides 15, 20, 24, 25 and 28.

Set expectations honestly:
  "Three demos. And you will not need to memorise a single IL opcode — I'll show you
   IL, but the goal is recognition, not recall."

TRANSITION: "So let's start with the obvious question: what IS metaprogramming?"
-> Slide 3.
```

## Slide 3 — What is metaprogramming?

**1:00** · Part 1

```text
[1:00]  DEFINITION — get the mental model in place; everything else hangs off it.

Beginner framing: "Normal code says 'take this customer's email and send a message.'
Metaprogramming says 'look at whatever object I just handed you, find out what
properties it has, and print each one.' The second sentence never mentions customers."

Walk the code box: top half is checked by the compiler. Bottom half — if I typo
"Emial", it still compiles, and it returns null at run time. Hold that thought; it is
the central trade of the whole session.

Then the blue box, and this is THE most important sentence of Part 1:
  "A .NET assembly is self-describing. The metadata isn't debug info and it isn't
   optional — the runtime needs it to resolve calls and collect garbage correctly.
   Reflection isn't magic; it's reading a table of contents that was always there."

Common confusion to pre-empt: "This is not `dynamic`. `dynamic` defers binding, but you
still write the member name in your source. Here the name itself can come from a config
file or a database row."

TRANSITION: "Let me make the difference concrete." -> Slide 4 (diagram).
```

## Slide 4 — Normal programming vs metaprogramming

**1:00** · Part 1

```text
[1:00]  DIAGRAM — the fork in the road.

Walk the LEFT column first, top to bottom:
  "customer.Email reads a VALUE. It works, it's fast, the compiler verified it — and it
   only ever works for Customer, because I typed the property name myself."

Then the RIGHT column:
  "GetProperty(name).GetValue(obj) reads the STRUCTURE. Same result, but now it works
   for Customer, Product, Order — and for a type someone writes next year."

Then the honest point at the bottom, and say it in your own words:
  "Notice what we gave up. On the left, a typo is a build error. On the right, a typo is
   a null at 3am. That's the trade. Everything else in this talk is a variation on it."

If asked "why would I ever choose the right-hand side?" — hold the answer; slide 5 is
exactly that question. Say "that's the next slide" and move.

TRANSITION: "So why would anyone accept that trade? Because sometimes the left-hand
column is impossible." -> Slide 5.
```

## Slide 5 — Why do we need it? Start with the problem

**0:50** · Part 1

```text
[0:50]  PROBLEM FIRST — this is the slide that earns the audience's trust.

Left side, say the costs out loud and land on the last one:
  "Forty methods. Volume is annoying. Drift is worse — add a property and the audit log
   is quietly incomplete, with no compiler warning. But look at the third bullet: a type
   that arrives in a DLL at run time. You CANNOT write that method in advance. That's
   not inconvenience, that's impossibility. And impossibility is what actually justifies
   reflection."

Right side: five lines replace forty methods. Pause there, briefly.

Then — and do not skip this — read the amber box. It is what separates a useful talk
from a dangerous one:
  "If all forty types are in your solution at build time, a source generator is strictly
   better: zero runtime cost, fully type-checked, works under AOT. Reflection earns its
   place when the type set is OPEN."

If you are running late: cut the amber box to one sentence ("generics or a source
generator are better when the types are known"), but do not cut it entirely.

TRANSITION: "So what do we actually get, and when does it happen?" -> Slide 6.
```

## Slide 6 — Benefits — and the three stages that get confused

**1:00** · Part 1

```text
[1:00]  BENEFITS + THE THREE STAGES. Two things on one slide; the diagram leads.

Walk the pipeline left to right, and name each stage as you point at it:
  1. "Source generators run HERE, during the build. They add real C# before the compiler
      finishes. At run time they don't exist and cost nothing."
  2. "Reflection reads the metadata box. Part 2."
  3. "Reflection.Emit adds NEW IL here, while the process is running. Part 3."

Then: "Keeping those three apart is the single most useful thing you can take from
Part 1. People say 'metaprogramming' and mean any of them."

Benefits: read only the fourth one slowly — "Extensibility is the only one of these you
cannot get another way. Automation and less boilerplate? A source generator does those
better. Extensibility — a plugin whose type didn't exist at build time — needs runtime
metaprogramming."

Amber column: say "and I'd rather you left knowing when NOT to do this." Read two of the
four; let them read the rest.

TRANSITION: "One last idea before we open the API: where does metadata come from?"
-> Slide 7.
```

## Slide 7 — Implicit vs explicit metadata

**0:40** · Part 1

```text
[0:40]  IMPLICIT vs EXPLICIT — short slide, one precision point. Keep it tight.

"I wrote no metadata here. But names, types, accessibility, parameter names — all
queryable, because the compiler had to emit them. That's implicit metadata."

"The attributes are different. I chose to put those there. That's explicit."

Then the console output: "Same reflection API reads both. The only difference is who put
it there — the compiler, or me."

NOW the amber box, and be precise, because this is where careless talks mislead people:
  "I want to flag that 'implicit vs explicit' is a distinction I'm using to teach. It is
   NOT a CLR category. If you go and read ECMA-335 you will not find an 'implicit
   metadata' table — you'll find metadata tables, one of which is CustomAttribute. The
   split is about authorship, not mechanism."

A nice detail if you have five spare seconds: the compiler itself applies attributes —
NullableAttribute, for instance — which rather proves the line is about authorship.

TRANSITION: "That's Part 1. Now let's actually read this metadata." -> Slide 8.
```

## Slide 8 — What is runtime introspection?

**0:50** · Part 2

```text
[0:50]  INTROSPECTION — orient, then move. Part 2 is dense; do not linger here.

"Introspection is a program asking questions about itself and getting answers. And it
answers them from the metadata we just talked about — it is NOT reading your .cs files.
Source isn't deployed and isn't needed."

Point at the code: "That's six lines, and it is the whole API in spirit. Assembly, then
Type, then member. Everything else is detail."

The amber box is a limit people assume away, so state it explicitly:
  "Metadata describes signatures, not bodies. I can see that a Withdraw method exists
   and takes a decimal. I cannot ask 'does it check for a negative amount?' — that's IL,
   and reading IL is a different job."

Blue box: read three names, then say "if you've used any of these, you've shipped
reflection to production already."

TRANSITION: "To use the API well you need the shape of the metadata. It's a hierarchy."
-> Slide 9.
```

## Slide 9 — Understanding .NET metadata

**1:20** · Part 2

```text
[1:20]  METADATA STRUCTURE — the map for the rest of Part 2. Worth the time.

Walk the containment outside-in, pointing as you go:
  "Assembly — the unit of deployment and versioning. Inside it, a manifest: its own
   identity and the assemblies it expects. Then a module — almost always exactly one, so
   in practice assembly equals module. Inside that, types. Inside types, members."

Then the member row: "Constructor, method, property, field, event, nested type. Each of
those is a metadata table, and each has a matching *Info class in System.Reflection.
The API mirrors this picture exactly — which is why the API is easy once you have this
picture."

Two details that pay off later:
  • The PURPLE bar: "every level can carry custom attributes. That's Part 2's section 8,
    and it's how ASP.NET Core and EF Core are configured."
  • A property is NOT storage. "In metadata a property is a name bound to accessor
    METHODS. When we look at reflection output later and you see get_Salary in the
    method list — that's why."

Close with the blue bar, read it: "Metadata is required, not optional. The CLR needs it
to do its own job. That's the reason reflection can exist at all — and the reason you
cannot strip it out to save space."

If running late: skip the PE/stream internals entirely. They're in the study guide.

TRANSITION: "Here's the API that reads it." -> Slide 10.
```

## Slide 10 — The Reflection API

**1:10** · Part 2

```text
[1:10]  THE API — five steps and six types. Point, don't read.

Walk the workflow: "Get a Type. Ask it for members. You get *Info objects back. Then act
— GetValue, or Invoke. That's it. Five steps."

Now the amber note in the diagram, and say it with emphasis:
  "Cache step three. GetProperties() allocates a fresh array every single call. Calling
   it inside a loop is the number one reflection performance bug I see in code review,
   and it has nothing to do with reflection being slow in principle."

The table: don't read it. Say "you'll meet all six of these; the pattern is obvious once
you've seen two."

The two side boxes are the conceptual payoff:
  • "Everything derives from MemberInfo. That's why they all have Name and
     GetCustomAttributes — write one helper, it works on all of them."
  • "And the graph is navigable: a MethodInfo's ReturnType is a Type, which has members,
     which have types. From any node you can reach any other. That's what makes generic
     framework code possible at all."

TRANSITION: "Let's start at the top of that graph — finding a type." -> Slide 11.
```

## Slide 11 — Assembly and type discovery

**1:20** · Part 2

```text
[1:20]  TYPE DISCOVERY — one diagram, four APIs, one famous trap.

Diagram: "Assembly fans out to types; a type fans out to members. And note the arrow
style — finding all implementations of an interface is exactly how plugin discovery
works. We'll use it in four slides' time."

Four ways, and contrast the first two sharply:
  "typeof is compile-time. The compiler checks it; a typo is a build error; it's the
   fastest. ALWAYS prefer it. GetType() gives you the RUNTIME type of an object — if
   your variable is declared object but holds a SavingsAccount, GetType returns
   SavingsAccount. That distinction matters more than people expect."

Then the RED box. Slow down; this is the single most useful practical thing in Part 2:
  "Type.GetType looks in exactly two places: the assembly that CALLED it, and CoreLib.
   Anything else returns null. Not an exception — null. I've watched people lose an hour
   to this. If the type is in another assembly you need the assembly-qualified name."

Give them the fix: "Two habits. One: get the canonical string from
typeof(X).AssemblyQualifiedName and look at it. Two: pass throwOnError: true, so a
missing type is a named TypeLoadException instead of a null that explodes three frames
later."

One production detail if time allows: "GetTypes() THROWS ReflectionTypeLoadException if
any type fails to load — but the exception's .Types property still has the ones that did
load. Any host scanning assemblies it didn't build must catch that."

TRANSITION: "Once you have a type, you look inside it." -> Slide 12.
```

## Slide 12 — Inspecting members — and BindingFlags

**1:20** · Part 2

```text
[1:20]  MEMBERS + BindingFlags — two takeaways, both practical.

Run through the four families fast: constructors, properties, methods, fields. "Each
returns an array of *Info. You've seen the pattern already."

Takeaway 1 — the amber box. Point at the console output first:
  "GetMethods() with no arguments returned FOURTEEN methods on a class with four. Why?
   Because the default is Public | Instance | Static, and it includes everything
   inherited — ToString, Equals, GetHashCode, GetType, plus the base class, plus
   property accessors, which are real methods. Add DeclaredOnly and you get four."

Say the rule: "If a member you KNOW exists isn't in the result, check your flags before
you check your sanity. It is almost always the flags."

Takeaway 2 — the red box. This one tends to genuinely surprise people:
  "Look at the output: reflection found a PRIVATE method. With BindingFlags.NonPublic you
   can read and write private fields, and that is allowed — it's not a loophole.
   `private` is a rule the COMPILER enforces at compile time. Accessibility is an
   encapsulation feature. It is not a security boundary."

Pre-empt the obvious objection: "Legitimate uses — deserializers restoring objects with
no setters, ORMs, test fixtures. Illegitimate — routing around another library's
invariants, which breaks on their next patch release."

TRANSITION: "Reading is half of it. Now let's CREATE an object from a Type." -> Slide 13.
```

## Slide 13 — Dynamic object creation — Activator.CreateInstance

**1:20** · Part 2

```text
[1:20]  ACTIVATOR — the bridge from "a Type I discovered" to "a live object".

Open with why it exists: "`new Foo()` needs the compiler to know Foo. When the type name
came from a config file or a folder scan, there is no name to type. Activator is the
bridge."

Walk the top row: "Validate the type — not abstract, not an interface. Find the
constructors. Bind your object[] of arguments against their parameter types. Then
allocate and construct."

Now the red row, and this is the point of the slide:
  "Every one of those is a RUNTIME failure. `new SavingsAccount(...)` with wrong
   arguments is a BUILD error. The same mistake through Activator is a
   MissingMethodException when that code path finally executes — possibly in
   production, possibly on a Friday."

Two details that save real debugging time:
  • "The message for an abstract type says 'Constructor not found', which is
     misleading — the real problem is that it's abstract. Check IsAbstract yourself and
     throw a clear error. 'PluginX is abstract' is much kinder than 'constructor not
     found'."
  • "If the constructor itself throws, you get TargetInvocationException — the real
     exception is in InnerException. Always unwrap it, or every catch block upstream
     silently stops matching."

Bottom line: "new is one IL instruction, resolved at compile time. Activator validates,
searches, binds, then constructs. Use Activator only when the type genuinely isn't known
at compile time."

TRANSITION: "One more ingredient before we build something real: attributes." -> Slide 14.
```

## Slide 14 — Attributes and custom attributes

**1:30** · Part 2

```text
[1:30]  ATTRIBUTES — the richest slide in Part 2. Three points; budget your time.

Point 1 — the RED box. Lead with it, because it is the most common misconception:
  "An attribute does nothing. It's inert data. [JsonIgnore] has no behaviour whatsoever
   — System.Text.Json goes looking for it and decides to skip the property. If nothing
   reads an attribute, it's a comment the compiler happens to store."

Point 2 — the engine. Walk the code and the output:
  "I declare a ValidateRange attribute, I put it on a property, and one engine validates
   any annotated type. Look at the output: three different rules, three clear messages,
   and the engine's source never mentions the Payment class. Annotate a new class
   tomorrow and it just works. That is, in miniature, ASP.NET Core model validation."

Point 3 — the PURPLE box, which explains two things that confuse people:
  "The attribute OBJECT is not in your assembly. Only the recipe: the type, and the
   constructor arguments. GetCustomAttribute RUNS that constructor — which is why two
   calls return two DIFFERENT objects, and why attribute reads don't belong on a hot
   path. And it's why GetCustomAttributesData exists: it reads the raw row without
   running anything, which is what you want for an assembly you don't trust."

If time is short: the AttributeUsage table can be left for them to read. Just say
"three settings, and the Inherited one needs BOTH the attribute and the call site to
agree — that catches everyone once."

TRANSITION: "Interface, plus attribute, plus reflection, plus Activator. Put those four
together and you have a plugin system." -> Slide 15.
```

## Slide 15 — Practical example — dynamic plugin discovery

**1:10** · Part 2

```text
[1:10]  PLUGIN DISCOVERY — the payoff slide for Part 2. Everything so far, combined.

Architecture, three boxes: "A tiny CONTRACTS assembly that both sides reference. A host
that scans a folder. Plugin DLLs the host has NO compile-time reference to at all — in
the demo project that's enforced with ReferenceOutputAssembly=false, so the host
genuinely cannot see those types until it loads them."

The mechanism, and name the pieces as Part 2 concepts:
  "GetTypes to find candidates. IsAssignableFrom to check the interface.
   GetCustomAttribute to read the metadata. Activator to create it. Twenty lines. You've
   seen all four ingredients in the last fifteen minutes."

Point at the "ALC" box: "One AssemblyLoadContext per plugin. Two jobs — isolate each
plugin's private dependencies so two plugins can use two versions of the same library,
and DELIBERATELY NOT isolate the shared contract, so both sides get the same
IReportPlugin type. Get that second part wrong and you get an InvalidCastException whose
message names the same type twice. That baffling message is the signature symptom."

Now the RED bar. Say it clearly and do not soften it:
  "AssemblyLoadContext is NOT a security sandbox. It isolates type identity and
   versions. It does not isolate PRIVILEGES. Once you load an assembly, that code can
   read your files, open sockets, read your secrets, and call Environment.Exit. There is
   no supported in-process sandbox in modern .NET — Code Access Security and AppDomain
   partial trust are gone. If you must run untrusted code, use a process, a container,
   or a VM."

DEMO CUE (essential demo #1, ~40s): run 04-Plugin-Discovery. Show the two accepted
plugins and the two REJECTED ones — one disabled by attribute, one missing the attribute
entirely. "A good host tells you WHY it declined your plugin."

If behind schedule: skip the demo, point at the diagram, keep the red bar.

TRANSITION: "That's reading metadata. Now — can we WRITE code at run time?" -> Slide 16.
```

## Slide 16 — What is runtime code generation?

**1:00** · Part 3

```text
[1:00]  OPENING PART 3 — frame it as the mirror of Part 2, and be honest about motive.

"Part 2 read a description that was already there. Part 3 writes code that wasn't."

The one good reason, said plainly: "Reflection is flexible but slow per call, because
every call re-does binding and access checks. If you're going to do the same reflective
operation a million times, pay the reflection ONCE to generate a specialised method, then
call that. You convert a per-call cost into a one-off cost. That's it. That's the reason."

The structural reason: "A mock object must BE an IFoo to be passed where an IFoo is
expected. No amount of reflection creates a type. Only code generation does."

Then the table — and this is the constraint that decides whether any of Part 3 is
available to you:
  "Two flags, and they're different. Supported: can I create code at all? FALSE under
   Native AOT — Reflection.Emit throws PlatformNotSupportedException. Compiled: will it
   be JIT-compiled? FALSE on the Mono interpreter, so on iOS it works but runs
   interpreted — and the entire speed benefit, the only reason you did it, disappears."

Amber box: "So if you write a library that emits IL, check the flag and keep a
reflection fallback. That's not theoretical — it's what System.Text.Json and the DI
container actually do, which is why they still work under AOT."

TRANSITION: "Before the IL, let's be precise about how these two APIs differ."
-> Slide 17.
```

## Slide 17 — Reflection vs Reflection.Emit

**1:20** · Part 3

```text
[1:20]  REFLECTION vs EMIT — the comparison the syllabus asks for, plus the measured
answer to the question everyone has.

Walk the two lanes:
  "Top lane, reflection: for every single call it binds arguments, runs access checks,
   boxes things. The work REPEATS. Measured on my machine: about 7 to 8 times a direct
   property access."

Immediately defuse the panic: "But look at the units. 67 milliseconds for TWO MILLION
reads — that's about 33 nanoseconds per call. If you do this once per web request, it is
free. If you do it per row of a million-row import, it's your bottleneck. 'Reflection is
slow' is true per call and frequently irrelevant."

  "Bottom lane: the work happens ONCE. After that you're calling a delegate, about 1.2
   times direct. Same speed class as hand-written code."

Now the blue note at the bottom of the diagram — the most useful sentence in Part 3:
  "And here's the finding that surprised me when I measured it. A cached delegate from
   MethodInfo.CreateDelegate was EXACTLY as fast as the emitted IL. Identical. So before
   you write a single OpCode — try CreateDelegate. You get emit-level speed with none of
   the risk, none of the unverified IL, and code your colleagues can read."

The side box reframes the whole thing: "These aren't rivals. Reflection finds the target,
Emit bakes it in. Every real emitter I've read starts with a GetMethod call."

TRANSITION: "To emit IL you need to know a little IL. Let's build that up." -> Slide 18.
```

## Slide 18 — Introduction to CIL / MSIL

**1:30** · Part 3

```text
[1:30]  THE PIPELINE — the conceptual foundation for the next five slides.

Walk it left to right: "C# goes through csc and comes out as IL plus metadata. Note that
your DLL contains BOTH — Part 2 read the metadata box; Part 3 writes into the IL box. At
run time the JIT compiles IL to machine code, per method, on first call."

Answer the obvious question before it's asked: "Why the extra step? Because one assembly
runs on x64 and Arm64, Windows and Linux. The JIT specialises for the actual CPU — it
can use AVX-512 on a machine that has it without you shipping a separate build. And
because IL is typed and verifiable, the runtime can enforce memory safety."

Then the three branches, because the simple story isn't the whole story and this is
where accuracy matters:
  • "Native AOT: compiled fully ahead of time, NO JIT ships. So runtime code generation
     is impossible there — Emit throws. That's the constraint from slide 16, now with a
     reason attached."
  • "ReadyToRun: precompiled at publish time with IL kept as a fallback. Faster startup,
     and the JIT can still re-optimise."
  • "Mono interpreter: the IL is interpreted, never compiled."

One more worth mentioning if you have the breath: "tiered compilation is on by default,
which means a hot method gets compiled MORE than once — quickly first, then optimised.
It's why benchmark warm-up loops matter."

Close on the grey bar: "Part 2 reads the metadata. Part 3 writes new IL. Same file, two
halves."

TRANSITION: "So what does IL actually look like? It's a stack machine." -> Slide 19.
```

## Slide 19 — IL instructions and the evaluation stack

**2:00** · Part 3

```text
[2:00]  THE EVALUATION STACK — the longest slide in the deck, and worth every second.
If the audience leaves understanding only this, Part 3 succeeded.

Do NOT rush. Walk all four columns, pointing at the stack cells:

  "ldarg.0 — push argument zero. Stack now holds a.
   ldarg.1 — push argument one. Stack holds a, then b on top.
   add     — pop two, push their sum. Stack holds a plus b.
   ret     — pop the return value and return. Stack empty."

Then say: "That is a complete method. int Add(int a, int b). Four instructions. There
are no registers you can name in IL — everything goes through this stack."

Now the two rules in the blue bar, and frame them as the debugging tool they are:
  "Rule one: at ret, the stack holds EXACTLY the return value. Leave something behind and
   the IL is invalid. Rule two: operand types must suit the opcode.
   Nearly every Emit bug you will ever write is a violation of one of those two. Which is
   why I write the expected stack state as a comment on every single Emit line — it turns
   an invisible bug into a visible one."

Then the trap that catches everyone exactly once — give it its own beat:
  "One numbering detail. In a STATIC method, ldarg.0 is the first argument. In an
   INSTANCE method, ldarg.0 is `this`, and the first argument is ldarg.1. If your
   generated method returns nonsense or crashes, check this first."

The table: don't read it. "Eight instructions. That's genuinely enough to follow
everything in this part — and the goal is recognition, not recall."

Honest aside if someone looks sceptical: "You will probably never write IL by hand. But
being able to READ it changes how you understand the language — you can see exactly what
a lambda or a record costs."

TRANSITION: "Now let's generate one of these for real." -> Slide 20.
```

## Slide 20 — DynamicMethod, ILGenerator and CreateDelegate

**1:40** · Part 3

```text
[1:40]  THE PIPELINE — six steps, two warnings.

Walk the six boxes: "Declare the signature. Get the IL writer. Emit your instructions.
CreateDelegate, which hands the IL to the runtime and gives you something callable. The
JIT compiles it on the FIRST call. After that you have a cached delegate running at
roughly direct speed."

Warning 1 — the red box, and note WHERE it sits in the pipeline:
  "Invalid IL is detected at the JIT step. Not at Emit. Not at CreateDelegate. On the
   first CALL. So a bug in your IL surfaces a long way from the line that wrote it."

Warning 2 — the amber box, said with feeling, because it's the mistake that makes this
whole technique backfire:
  "CACHE the delegate. I have seen code that builds a DynamicMethod inside the method it
   is trying to speed up. That is slower than the reflection it replaced — strictly
   worse on every axis. Generate once. Cache. Reuse."

Then the grey bar, which is the most sobering thing I found while preparing this, so
deliver it as a finding, not a factoid:
  "I tested two broken methods on .NET 10. A missing `ret` threw InvalidProgramException
   on first call — fine, that's a clear failure. But `add` on an int and a string threw
   NOTHING. It returned a plausible-looking number, the same number twice. It was the
   string's managed reference added to 1 as an integer. Fully-trusted DynamicMethod IL is
   NOT type-verified. Your safety net is thinner than the C# compiler's, and sometimes
   there is no net at all. That asymmetry is the real reason to be careful here."

TRANSITION: "Let's see it work." -> Slide 21 (demo).
```

## Slide 21 — Practical example — generating a method at runtime

**2:00** · Part 3

```text
[2:00]  DEMO SLIDE (essential demo #2). Budget: ~50s talking, ~60s demo.

Walk the code line by line — they have just learned these four instructions, so this
should feel like recognition rather than novelty:
  "Declare the signature. Get the generator. ldarg.0, ldarg.1, add, ret — exactly the
   four from the last slide, and I've written the stack state in the comments.
   CreateDelegate. And now it's an ordinary Func I can call."

The purple box is the conceptual point, so don't skip it:
  "Notice the opcode is just DATA. Which means one factory can build plus, minus, times
   and divide from an operator string that didn't exist at compile time. That is the
   honest shape of this technique: a small interpreter of YOUR data, turned into machine
   code once, then reused. For four operators a switch statement is simpler — the payoff
   comes when the 'data' is a whole mapping configuration or a type's property list."

DEMO: run 05-DynamicMethod in RELEASE. Show the stack-state table and the benchmark.

Then the measurement, and make the four readings explicit:
  1. "Reflection is 7 to 8 times direct — but 33 nanoseconds a call."
  2. "Cached delegate and emitted IL are IDENTICAL at 1.2x. Caching the work is what
      wins, not IL specifically."
  3. "So try CreateDelegate first. Always."
  4. "And look at the last two rows — same property, same technique, only the signature
      differs. object-to-object pays a cast and a decimal box every call, and THAT costs
      more than the choice between reflection and emit. People optimise 'reflection vs
      emit' while ignoring boxing. It's the wrong axis."

Caveat, stated plainly: "Stopwatch in a loop, one machine, one run. Honest to an order of
magnitude and no better. Use BenchmarkDotNet for anything you'll act on."

TRANSITION: "A single method is useful. Sometimes you need a whole TYPE." -> Slide 22.
```

## Slide 22 — AssemblyBuilder, ModuleBuilder, TypeBuilder, MethodBuilder

**1:30** · Part 3

```text
[1:30]  THE BUILDERS — the hierarchy, the design pattern, and one important correction.

"This is Part 2's metadata hierarchy, built in reverse. Part 2 READ Assembly, Type,
Member. Here we WRITE the same structure: AssemblyBuilder, ModuleBuilder, TypeBuilder,
and then fields, constructors, methods and properties — each with its own ILGenerator."

One rule governs all of it: "CreateType() BAKES the type. Before that call it doesn't
exist and can't be instantiated. After it, nothing more can be added."

Now point at AddInterfaceImplementation, because this is the design pattern that makes
the whole thing practical:
  "Generate a type that implements an interface you ALREADY KNOW, then talk to it through
   that interface. Reflection is used ONCE, at creation. Never on the hot path. That's
   not a trick — it's exactly how Moq, NSubstitute, Castle DynamicProxy and EF Core's
   lazy-loading proxies all work."

Then the amber bar, and flag it as a correction to material they may have read:
  "A warning about older tutorials. On .NET 10, AssemblyBuilderAccess has exactly TWO
   members: Run and RunAndCollect. Save and RunAndSave were .NET FRAMEWORK members. They
   do not exist. If you've seen a tutorial doing DefineDynamicAssembly with RunAndSave
   and then calling .Save — that code does not compile on modern .NET at all. I checked
   this by printing the live enum members, and the demo does that on screen."

Then the good news: "Saving IS supported — through a different class,
PersistedAssemblyBuilder. It's in the shared framework, no package needed. Next slide."

TRANSITION: "Let's generate a type and then save it to disk." -> Slide 23.
```

## Slide 23 — Generating types and methods at runtime

**1:00** · Part 3

```text
[1:00]  GENERATED TYPE, END TO END. Tight slide — the heavy lifting was slide 22.

Walk the code quickly: "Define the type. Declare the interface. Define a private
readonly field — FieldAttributes.InitOnly IS readonly. Then the method body: load this,
load the field, load the argument, multiply, return. Then CreateType."

Point at the console output, top half: "And now the payoff — `scaler.Scale(6)`. That is
an ordinary C# interface call on a type that did not exist when this program started. No
reflection at the call site. And reflection can inspect it: there's the private readonly
field, exactly as declared."

Bottom half of the output, the persistence result:
  "And this is the thing I'd most like to correct from older material. That's a real
   2-kilobyte DLL on disk, written with PersistedAssemblyBuilder, loaded back with
   Assembly.LoadFrom, and invoked. IsDynamic is False because it came from a file.
   Add(20,22) returned 42."

Say why persistence actually matters, practically: "This turns IL debugging from guesswork
into inspection. Save the assembly, open it in ILSpy, and READ the IL you emitted. Or run
ILVerify over it and get proper validation instead of waiting for
InvalidProgramException on the first call."

Then the amber box — a 10-second callback that cements slide 19's trap:
  "And notice ldarg.0 here. It's `this`, because this is an instance method. On slide 21
   the method was static and ldarg.0 was the first argument. Same opcode, different
   meaning. That's the one I promised would catch you."

TRANSITION: "You now have everything you need to recognise this machinery in the
frameworks you use every day." -> Slide 24.
```

## Slide 24 — ASP.NET Core MVC and dependency injection

**2:15** · Part 4

```text
[2:15]  MVC + DI — the biggest slide in Part 4. Two topics, one lesson.

MVC first (~60s). "You write a class called ReportsController with [HttpGet] on a
method. Program.cs contains ONE relevant line — AddControllers() — and it names no
controller. How does the framework find yours?"

Name the three mechanisms as Part 2 concepts:
  • "Application parts are the SEARCH PATH — by default the entry assembly. Add a part
     and MVC finds controllers in another DLL. That's how a plugin contributes endpoints."
  • "Then it scans for types. The rule: it's a controller if it carries [ApiController]
     or [Controller], OR if its NAME ends with 'Controller'. Note what's NOT in that
     rule — inheriting from ControllerBase. That surprises people. ControllerBase just
     gives you Ok() and NotFound()."
  • "Then it reads [HttpGet] and [Route] into an ActionDescriptor table."

DI second (~45s). The single insight: "A constructor signature IS a machine-readable
dependency list. Nobody wrote a config file saying OrderService needs a repository and a
clock — the constructor already says so, and the constructor is already in metadata.
That's the whole idea."

Three things to mention and move on: "Lifetimes — singleton, scoped which is per HTTP
request, transient. The classic bug is a singleton capturing a scoped service; turn on
ValidateScopes and it becomes an exception instead of a slow leak. And the container
picks the greediest SATISFIABLE constructor — so adding a registration elsewhere can
change which constructor runs. One constructor per class avoids the whole question."

Then be precise where most talks over-claim:
  "You'll hear 'DI uses Reflection.Emit'. I read the provider's private engine field on
   .NET 10 — it's DynamicServiceProviderEngine, which picks a strategy at run time: a
   reflection engine first, a faster compiled one once a service is resolved enough
   times, and reflection-only under AOT. So 'DI uses reflection' is the safe claim.
   Anything more specific depends on the container, its version and the platform."

Land the blue bar — the thesis of the whole talk, third appearance:
  "Reflection at STARTUP. Per request it's a table lookup and a cached delegate. DISCOVER
   ONCE, CACHE, THEN EXECUTE COMPILED CODE. Which is why 'MVC uses reflection so it must
   be slow per request' is simply wrong."

If behind: cut the ValidateScopes detail and the greediest-constructor detail.

TRANSITION: "Same pattern, two more frameworks." -> Slide 25.
```

## Slide 25 — Entity Framework Core and serialization

**2:00** · Part 4

```text
[2:00]  EF CORE + SERIALIZATION. Two topics; the EF half is the more impressive demo.

EF CORE (~70s). Point at the C#, then at the SQL:
  "Three lines of LINQ. And THAT is the SQL EF produced. The navigation p.Blog.Title
   became an INNER JOIN; the nullable p.Author.Name became a LEFT JOIN; the anonymous
   type became a SELECT list. It never loaded a whole Post entity."

How, in one sentence: "It works because IQueryable's Where takes
Expression<Func<T,bool>>, not Func<T,bool>. The query arrives as DATA, and EF runs a
chain of ExpressionVisitors over it until it can be printed as SQL."

The precision point the syllabus asks for — the model distinction:
  "And the EF model is NOT your classes. It's a separate, RICHER model that EF builds by
   READING your CLR metadata. The proof is a shadow property: a property that exists in
   EF's model with no C# property at all. There's a column in the database and nothing to
   assign in your class. Reflection is the input; the model is the output."

The amber box is a good credibility detail: "Look at how string.Contains was rendered —
instr(...), a SQLite function, not LIKE. The PROVIDER chooses the translation. SQL Server
emits something different for the same C#. So read the SQL your provider actually
produces — that's what ToQueryString is for."

SERIALIZATION (~45s). "Same job, two mechanisms, and this is slide 6 made concrete."
Point at 'Byte-for-byte identical: True': "Same input, same output. The only difference
is WHEN the metaprogramming ran — run time, or during the build."

Then the table: "And the right-hand column is why source generation exists: no startup
cost, trimming-safe, and it works under Native AOT where reflection mode doesn't."

The measurement box, if time allows — it corrects advice they've probably heard:
  "The usual advice is 'reuse your JsonSerializerOptions because otherwise it re-reflects
   every call'. I measured it three ways. New-but-identical options is only 1.4x, because
   System.Text.Json shares a process-wide cache between EQUIVALENT instances. But new
   options with DIFFERENT settings is 7.2x. So the correct advice is: reuse it, and above
   all don't VARY its settings per call. The second mistake is the expensive one."

If behind schedule: cut the JSON measurement box entirely and just say "reuse your
options instance."

TRANSITION: "Two more places this shows up." -> Slide 26.
```

## Slide 26 — Plugin architectures and dynamic proxies

**1:30** · Part 4

```text
[1:30]  PLUGINS + PROXIES. Plugins are a recap; proxies are new. Weight it ~25/65.

PROXIES (~55s). Walk the diagram left to right:
  "The caller holds an IAccountService. What it actually has is generatedProxy_1 — a type
   created at RUN TIME that implements the interface and derives from our LoggingProxy.
   Every single interface call lands in ONE method: Invoke. That's where logging, caching,
   retries and timing go."

Then the key realisation: "DispatchProxy is doing exactly what we did by hand on slide
22 — define a type at run time, implement a known interface, create an instance. It
wrote the TypeBuilder code for us."

Show the value: "AccountService.cs contains no logging code at all. None. The
cross-cutting concern moved out of the business logic entirely."

Then — important, and the syllabus asks for it — the honest caveat:
  "But be clear about what metaprogramming gave us. It gave us the SEAM. It did not give
   us the POLICY. The demo cache in the repo has no expiry, no size limit, no
   invalidation and no thread safety. A real one needs all four. Same for authorization:
   a proxy can be WHERE you enforce a policy; it doesn't supply the policy or make it
   correct. An interception mechanism that silently applies a wrong policy is worse than
   no interception."

The limits list — read two, especially the third:
  "Interfaces only. And the one that catches everyone: a SELF-CALL inside the service is
   NOT intercepted, because `this` inside the service is the real object, not the proxy.
   That breaks naive caching in a way that's very hard to spot."

PLUGINS (~25s) — pure recap of slide 15. Just the three bullets, and repeat the red one:
"AssemblyLoadContext is not a sandbox." Say it twice in the talk; it's the thing people
most often get wrong.

If behind: drop the plugin bullets entirely, keep the proxy diagram and the seam/policy
point.

TRANSITION: "Which brings us to the last technique — and the one I'd actually recommend."
-> Slide 27.
```

## Slide 27 — Cross-cutting concerns and expression trees

**1:45** · Part 4

```text
[1:45]  EXPRESSION TREES + CHOOSING A SEAM. End Part 4 on the recommendation.

EXPRESSION TREES (~65s). Start with the code box, because the point is startling:
  "Two lines. IDENTICAL source text — n => n > 18. The only difference is the TYPE I
   assigned it to. Func gives you compiled IL: you can call it, you cannot read it.
   Expression gives you an object GRAPH describing the lambda: you can read it, rewrite
   it, print it, translate it."

Walk the tree: "AndAlso at the root. Underneath, GreaterThan and Equal. Under those,
MemberAccess nodes for c.Age and c.Country, and Constant nodes for 18 and "IN". Every
node is a strongly typed object. Which means a library can ask 'which property is being
compared, and to what?' — and that is EXACTLY the question a SQL translator has to
answer."

Then close the loop with the previous slide: "This is how the EF Core SQL on slide 25
happened. Same mechanism."

Now the comparison with Part 3, and make the recommendation explicit:
  "Compare this with Reflection.Emit. Same speed class after compilation. But the
   expression version is type-checked AS YOU BUILD IT — Expression.Add on an int and a
   string throws immediately, with a clear message. Remember what the IL version did in
   that situation? It silently returned garbage. So: prefer expression trees whenever you
   need a FUNCTION. Reach for Emit only when you need a TYPE, which Expression cannot
   create."

One precision point worth 10 seconds: "Two different failures get confused here. 'Cannot
be expressed as a tree' — statement bodies, await, assignment — is a C# COMPILE error.
'Cannot be translated to SQL' is a provider exception at RUN time. Different failures,
different fixes."

SEAMS (~35s). "Choose by where the concern belongs. The test: would this still apply if
the call came from a background job instead of HTTP? If yes, it belongs at the service
seam, not in middleware."

Then end on the honest recommendation, and mean it:
  "And the answer that's right more often than any of them: a hand-written decorator
   class. Twenty lines of ordinary C#. Debuggable, trimmable, AOT-safe, obvious to the
   next person. Reach for a generated proxy when the number of interfaces makes
   hand-writing them genuinely impractical — not before."

TRANSITION: "Let me leave you with five things." -> Slide 28.
```

## Slide 28 — Questions

**1:30** · Closing

```text
[1:30]  CLOSING — five takeaways, then open the floor. Do not add new material here.

Read the five bullets. Pause after the second and the fourth.

  1. "Self-describing assemblies. That's the foundation — metadata is required, not
      optional, and reflection just reads it."
  2. "Discover once, cache, execute compiled code. If you remember one sentence from
      37 minutes, make it that one. MVC, DI, EF Core and System.Text.Json all do
      exactly this."
  3. "Try CreateDelegate before you write IL. I measured it: identical speed to emitted
      IL, and none of the unverified-IL risk."
  4. "Expression trees for a function. Emit only for a type."
  5. "And the trade, stated plainly: compile-time safety for run-time flexibility. Make
      it when you need the flexibility — and not one step sooner."

The decision order: "Work down that list and stop at the first thing that works. Most
people who reach for step five needed step one or two."

The three corrections box: read all three aloud. They are the claims most likely to be
repeated wrongly, and you want the room leaving with them right.

Then: "Everything's in the repo — the code, a study guide, the diagrams, and every
console output you saw captured as a text file so you can diff your own run."

Open with: "What would you like to dig into?"

LIKELY QUESTIONS — short answers ready (full versions in Presenter-Guide/Audience-QA.md):
  • "Is reflection slow?" -> per call yes, ~33ns; cache and it stops mattering.
  • "Can Emit work with Native AOT?" -> No. IsDynamicCodeSupported is false, it throws.
    Use source generators.
  • "Can I unload a plugin?" -> Loading yes; unloading needs a collectible context and
    ZERO remaining references, and it's cooperative. Most systems use a child process.
  • "Is ALC a security boundary?" -> No. Type identity, not privileges.
  • "Reflection vs expression trees?" -> Reflection describes DECLARATIONS; expression
    trees represent CODE.
  • "Can reflection access private members?" -> Yes, and that's allowed. private is a
    compiler rule, not a security boundary.

If you do not know: "I don't want to guess — that's exactly the kind of thing that
changes between versions. Let me check and come back to you." Then actually do.
```
