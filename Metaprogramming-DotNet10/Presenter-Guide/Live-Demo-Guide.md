# Live Demo Guide

A 37-minute session cannot run twelve demos. **Two are essential, two are optional.**
Everything else stays in the repo for people to run afterwards — and the captured output of
all twelve is in `Code-Examples/expected-output/`, so nobody has to take your word for
anything.

## The demo plan

| | Demo | Slide | Budget | Status | Covers |
|---|---|---|---|---|---|
| **A** | `04-Plugin-Discovery` | 15 | 0:40 | **ESSENTIAL** | reflection + type discovery + attribute discovery + `Activator` — four priorities in one run |
| **B** | `05-DynamicMethod` | 21 | 1:00 | **ESSENTIAL** | IL emission + `CreateDelegate` + the measured benchmark |
| C | `06-ReflectionEmit` | 23 | 0:45 | optional — only if ahead | `TypeBuilder` + the `PersistedAssemblyBuilder` proof |
| D | `11-EntityFrameworkCore` | 25 | 0:45 | optional — only if ahead | expression trees becoming real SQL |

**Total committed demo time: 1:40**, which is what the script's pacing assumes.

Why these two are the essential pair: demo A proves that twenty lines of reflection build a
real plugin system, and demo B proves the performance claim that the whole of Part 3 rests
on. Between them they cover five of the six prioritised demonstrations.

---

## Before you present — the 10-minute setup

Do this **the day before**, not in the room.

```bash
cd Code-Examples
dotnet restore Metaprogramming.slnx
dotnet build Metaprogramming.slnx -c Release
```

Expect `Build succeeded. 0 Warning(s) 0 Error(s)` for 14 projects (12 runnable examples + 2 libraries).

Then **run both essential demos once**, so every assembly is warm and NuGet has nothing
left to fetch:

```bash
dotnet run -c Release --no-build --project 04-Plugin-Discovery/PluginDiscovery.Host
dotnet run -c Release --no-build --project 05-DynamicMethod
```

### Terminal checklist

- [ ] Font size **18pt or larger**. Test it from the back of the room, not from your chair.
- [ ] High-contrast theme — light background if the projector is washed out.
- [ ] Window sized so **80 characters** fit without wrapping. The demo output is formatted
      for 80 columns; wrapping destroys the aligned tables.
- [ ] Working directory already set to `Code-Examples`.
- [ ] Commands already in your shell history (press ↑ rather than typing).
- [ ] Notifications **off**. Email, Teams, Slack, calendar — all of it.
- [ ] `--no-build` on every command, so a stray rebuild can't cost you 20 seconds.

### The fallback kit — build this, you will probably need it

1. Copy `Code-Examples/expected-output/04-Plugin-Discovery.txt` and
   `05-DynamicMethod.txt` somewhere you can open in one click.
2. Screenshot the two or three key screens from each.
3. Keep a text editor open on a second workspace with those files loaded.

> **The rule: if a demo does not work within 20 seconds, abandon it.** Say
> *"let me show you what it prints"*, switch to the captured output, and keep talking. The
> audience will not remember that the demo failed. They will absolutely remember three
> minutes of you fighting a terminal.

---

# Demo A — Plugin discovery · slide 15 · 0:40 · **ESSENTIAL**

## Command

**bash / zsh:**

```bash
dotnet run -c Release --no-build --project 04-Plugin-Discovery/PluginDiscovery.Host
```

**Windows PowerShell** — same command; only the path separator differs if you type it by
hand, and forward slashes work in PowerShell too:

```powershell
dotnet run -c Release --no-build --project 04-Plugin-Discovery\PluginDiscovery.Host
```

## What to show — scroll straight to section 2

Do **not** read sections 1, 3, 4 or 5 aloud. Scroll to the on-disk discovery output:

```text
Plugin folder: ...\PluginDiscovery.Host\bin\Release\net10.0\plugins
Candidate assemblies: PluginDiscovery.Plugins.Sales.dll

Loaded PluginDiscovery.Plugins.Sales v1.0.0.0
  into load context: PluginALC:PluginDiscovery.Plugins.Sales
  collectible      : True
  accepted MonthlySalesPlugin as "Monthly Sales Summary" v2.1.0
  accepted TopCustomersPlugin as "Top Customers" v2.1.0
  skipped ExperimentalForecastPlugin - [ReportPlugin(Enabled = false)]
  skipped UnannotatedHelperPlugin - implements IReportPlugin but has no [ReportPlugin]

  --- monthly-sales ---
    Monthly sales for 2026-11
      South 418,500.00
      ...
      (rendered by PluginDiscovery.Plugins.Sales v1.0.0.0)
```

## What the audience should observe — say these three things

1. **"The host has no compile-time reference to that assembly."** Point at
   `Candidate assemblies`. The host found a DLL on disk, loaded it, and ran code from it.
2. **"Look at the two rejections."** This is the bit people remember.
   `ExperimentalForecastPlugin` implements the interface *and* has the attribute — but the
   attribute says `Enabled = false`, so the host read metadata and declined **without
   constructing it**. `UnannotatedHelperPlugin` implements the interface but has no
   attribute. *"A good host tells you why it declined your plugin."*
3. **"Collectible: True."** That is what makes `Unload()` possible at all — and section 4
   of the output shows the context actually being collected, on the second GC attempt.

## If you have 15 extra seconds

Scroll to section 3 and read two lines:

```text
Same CLR type? True
Cast works?    True
```

*"Both sides got the same `IReportPlugin`, because the load context deliberately does NOT
isolate the shared contract. Get that wrong and you get an `InvalidCastException` whose
message names the same type twice."*

## Troubleshooting

| Symptom | Fix |
|---|---|
| `No plugins folder at ...` | You ran before building. `dotnet build` on `PluginDiscovery.Host` — a post-build target copies the plugin DLL into `bin/.../plugins`. |
| `Candidate assemblies: (none)` | Same cause. Check `bin/Release/net10.0/plugins/` exists. |
| Unload reports `alive = True` after 10 attempts | A debugger is attached. Run from a plain terminal, not F5. **Do not debug this live** — just say "the repo shows it collecting on the second attempt". |
| `InvalidCastException` naming the same type twice | Someone changed `Private="false"` on the contracts reference. That is the duplicated-contract bug — a great teaching moment, but not live. |

## Fallback line

> "It's doing the discovery now — here's the output from my run this morning."

Then open `expected-output/04-Plugin-Discovery.txt` and point at the same three things.
**Nothing is lost.** The output is identical.

---

# Demo B — DynamicMethod and the benchmark · slide 21 · 1:00 · **ESSENTIAL**

## Command — Release matters

```bash
dotnet run -c Release --no-build --project 05-DynamicMethod
```

> **Run this in Release.** In Debug the benchmark ratios are misleading, and you will be
> quoting numbers you then have to apologise for.

## What to show — two screens

**Screen 1 — the IL and the stack** (scroll to section 1):

```text
Signature: int Add(int a, int b)

  IL                 | evaluation stack after the instruction (top on the right)
  -------------------+-------------------------------------------------------
  ldarg.0            | [ a ]                 push argument 0
  ldarg.1            | [ a, b ]              push argument 1
  add                | [ a+b ]               pop two, push their sum
  ret                | [ ]                   pop the return value and return

add(2, 3)    = 5
add(40, 2)   = 42
```

**Screen 2 — the benchmark** (scroll to section 7):

```text
  technique                                total ms    relative to direct
  -----------------------------------------------------------------------
  direct C# property access                     8        1.0x
  PropertyInfo.GetValue                        67        8.4x
  MethodInfo.Invoke                            59        7.4x
  cached delegate (typed)                      10        1.2x
  emitted IL (typed, no box)                   10        1.2x
  emitted IL (object->object, boxes)           21        2.6x
```

> Your numbers will differ from these. **Say so.** "Different machine, different numbers —
> the ratios are the lesson."

## What the audience should observe — four readings, in this order

1. **"Reflection is 7 to 8 times direct — and that's 33 nanoseconds a call."** Both halves
   of that sentence matter. The ratio sounds alarming; the absolute number usually isn't.
2. **"Cached delegate and emitted IL are identical."** Rows 4 and 5. Caching the work is
   what wins — not IL.
3. **"So try `CreateDelegate` first."** The single most actionable line in Part 3.
4. **"Now compare the last two rows."** Same property, same technique, different signature.
   The `object → object` version pays a cast and a decimal box per call, and that costs more
   than the emit/reflection choice. *"People optimise the wrong axis."*

## If you have 20 extra seconds — the best 20 seconds in the session

Scroll to section 8, Case B:

```text
Case B: a stack TYPE mismatch - 'add' on an int32 and a string reference.
  No exception. broken(1) returned 299021057, and again 299021057.
```

> "No exception. A plausible-looking number, twice. That's the string's managed reference
> added to one, as an integer. Fully-trusted `DynamicMethod` IL is not type-verified."

That lands the "be careful with Emit" message far better than any slide can.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Ratios look wrong (reflection *faster*) | You are in Debug. Add `-c Release`. |
| The whole thing takes >15 s | 2,000,000 iterations plus warm-up. Normal. Talk over it — walk screen 1 while it runs. |
| `InvalidProgramException` crashes the app | It shouldn't — it is caught. If it escapes, you are on a modified build. Use the captured output. |
| Output wraps and the table is unreadable | Terminal narrower than 80 columns. Fix before you start. |

## Fallback line

> "The timings take a few seconds — here's a run from this morning, and your numbers will
> be in the same ratios."

---

# Demo C — Reflection.Emit and persistence · slide 23 · 0:45 · OPTIONAL

**Run this only if you reach slide 22 at or before 26:30.**

```bash
dotnet run -c Release --no-build --project 06-ReflectionEmit
```

## What to show — two things only

**The live enum members** (section 1) — this is the correction to older tutorials:

```text
AssemblyBuilderAccess members that exist on this runtime:
  AssemblyBuilderAccess.Run
  AssemblyBuilderAccess.RunAndCollect

Does the runtime AssemblyBuilder have a Save(string) method? no
```

> "Two members. `Save` and `RunAndSave` are not there. They were .NET Framework."

**The persisted DLL** (section 6):

```text
Wrote a real assembly to disk:
  path  : ...\Metaprogramming.Saved.dll
  exists: True
  bytes : 2048

Loaded that file back in and invoked it:
  IsDynamic : False  (false - it came from a file)
  Add(20,22)= 42
```

> "A real DLL. Loaded from the file. `IsDynamic` false. Forty-two."

**Skip** sections 2–5 and 7 entirely. They are for the study guide.

---

# Demo D — Expression trees becoming SQL · slide 25 · 0:45 · OPTIONAL

**Run this only if you reach slide 25 at or before 31:00.**

```bash
dotnet run -c Release --no-build --project 11-EntityFrameworkCore
```

Scroll to section 5B and show the C# alongside the SQL:

```text
SELECT "b"."Title", "b0"."Title" AS "Blog", "a"."full_name" AS "Author"
FROM "blog_posts" AS "b"
INNER JOIN "Blogs" AS "b0" ON "b"."BlogId" = "b0"."Id"
LEFT JOIN "authors" AS "a" ON "b"."AuthorId" = "a"."AuthorKey"
WHERE "b"."Rating" >= 0 AND "b"."Rating" >= 4 AND instr("b0"."Title", '.NET') > 0
```

> "Three lines of LINQ became that. And note `instr` — a SQLite function, not `LIKE`. The
> provider chooses."

No database setup is needed; it uses SQLite **in-memory**.

If you have another 15 seconds, section 6 is the counterpart:

```text
  InvalidOperationException:
    The LINQ expression '...Where(p => Program.IsProofRead(p.Content))' could not be translated
```

> "And EF refuses rather than silently downloading the whole table."

---

# Kept back for independent study

These are in the repo, fully explained, with captured output. Mention in your closing that
they exist; do not run them.

| Project | Why it is not a live demo |
|---|---|
| `01-Basics` | conceptual — the slides cover it better than a terminal |
| `02-Reflection` | 164 lines of output; demo A shows the same APIs in action |
| `03-Attributes` | demo A already shows attribute-driven discovery |
| `07-DependencyInjection` | the engine-inspection section is a great read, a poor demo |
| `08-ExpressionTrees` | the tree diagram on slide 27 is clearer than the console |
| `09-Serialization` | the "byte-for-byte identical: True" line is already on slide 25 |
| `10-ASPNetCoreMVC` | needs a port; `--selftest` makes it safe, but it costs 30 s to start |
| `12-DynamicProxies` | 248 lines; slide 26's diagram carries the idea |

If someone asks for one of these in Q&A, run it then — you will have the time and the
audience has asked for it, which is the best possible demo.

---

# The universal fallback plan

Have these three things ready **before** you start:

1. **`Code-Examples/expected-output/`** open in an editor on a second workspace. Twelve
   text files, the complete captured output of every example.
2. **Screenshots** of: demo A's accept/reject block, demo B's stack table, demo B's
   benchmark table. Three images.
3. **A sentence you have already rehearsed:**

> "Rather than fight this live, let me show you exactly what it prints — and the full
> transcript is in the repo so you can run it yourself tonight."

## The three rules

1. **20-second limit.** Abandon any demo that is not working. No exceptions.
2. **Never debug live.** Not a typo, not a path, not a port. Switch to the output.
3. **Never apologise more than once.** One "let me show you the output instead" and move
   on. Repeated apologising costs more credibility than the failure did.

## And one thing in your favour

Every claim in this deck is backed by a captured transcript in the repo. If a demo fails,
you lose a piece of theatre — you do not lose the argument. Say so, and keep going.
