# Troubleshooting

Everything that can go wrong, and what to do about it. Organised by when it bites you.

---

# 1. Setup — the day before

## SDK and build

| Symptom | Cause | Fix |
|---|---|---|
| `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` | SDK older than 10.0 | Install the .NET 10 SDK. Check with `dotnet --list-sdks` — you need a `10.x` entry. |
| `dotnet --version` shows 8.x although 10 is installed | A `global.json` upstream pins the SDK | Look for `global.json` in the directory tree above `Code-Examples` and remove or update it. |
| `NU1202: Package ... is not compatible with net10.0` | A package was bumped to an 11.x version | The newest EF Core for `net10.0` is **10.0.15**; 11.x requires `net11.0`. Pin versions in `Code-Examples/Directory.Build.props`. |
| Restore hangs or fails | No network, or a private feed is unreachable | `dotnet nuget list source`. Only nuget.org is needed. Offline? See "No network" below. |
| `Metaprogramming.slnx` not recognised | SDK too old for the `.slnx` solution format | Use .NET 10. Or build projects individually — every example works standalone. |
| Build warnings you didn't expect | A modified source file | A clean checkout builds with **0 warnings, 0 errors** for 14 projects (12 runnable examples + 2 libraries). |

## No network at the venue

Restore **before** you travel:

```bash
cd Code-Examples
dotnet restore Metaprogramming.slnx
dotnet build Metaprogramming.slnx -c Release
```

After that, always run with `--no-build`, and nothing touches the network. Only two projects
need packages at all (`07-DependencyInjection`, `11-EntityFrameworkCore` — plus
`12-DynamicProxies`); everything else uses the shared framework.

## Verify you are ready

```bash
dotnet run -c Release --no-build --project 04-Plugin-Discovery/PluginDiscovery.Host
dotnet run -c Release --no-build --project 05-DynamicMethod
```

If both print without error, you are set. Compare against
`Code-Examples/expected-output/` if anything looks odd.

---

# 2. The presentation file

| Symptom | Fix |
|---|---|
| The deck won't open | It is a standard `.pptx`. Try PowerPoint, LibreOffice Impress, Google Slides, or Keynote. If one refuses, another will not. |
| Fonts look wrong | The deck uses **Segoe UI** and **Consolas**. On macOS or Linux they substitute. Nothing will be clipped — the title and code sizes are computed to fit — but spacing shifts slightly. To be safe, present from Windows, or export to PDF first. |
| Speaker notes are missing | They are on all 28 slides. In PowerPoint: View → Notes Page, or Presenter View. If your viewer hides them, read [`../Presentation/Speaker-Notes.md`](../Presentation/Speaker-Notes.md) instead — same text. |
| A diagram looks wrong after editing | Diagrams are **native shapes**, so they are editable — and therefore breakable. Re-copy the slide from a fresh download, or rebuild the diagram from [`../Assets/Diagrams/`](../Assets/Diagrams/). |
| Text looks small on the projector | Nothing in the deck renders below **9 pt**, which is small-but-legible at 16:9. If the room is large, present the PDF zoomed, or drop the two most detailed slides (14, 25) and talk from the diagrams. |
| 4:3 projector | The deck is 16:9. Let it letterbox — do **not** rescale to 4:3, which will distort the diagrams. |

---

# 3. During a demo

> **The 20-second rule: if a demo isn't working in 20 seconds, abandon it.** Switch to
> `Code-Examples/expected-output/`. See [`Live-Demo-Guide.md`](Live-Demo-Guide.md).

## Demo A — plugin discovery

| Symptom | Fix |
|---|---|
| `No plugins folder at ...` | You ran before building. `dotnet build` on `PluginDiscovery.Host`; a post-build target copies the plugin DLL into `bin/.../plugins`. |
| `Candidate assemblies: (none)` | Same cause. Check `bin/Release/net10.0/plugins/` exists and contains `PluginDiscovery.Plugins.Sales.dll`. |
| Unload reports `alive = True` after 10 attempts | A debugger is attached, or the JIT kept a local rooted. Run from a plain terminal, never under F5. Don't debug this live. |
| `InvalidCastException` naming the same type twice | The contracts assembly got duplicated into the plugin's own folder — someone changed `Private="false"`. A wonderful teaching moment; a terrible live one. |
| Plugin output is missing but no error | The plugin's `[ReportPlugin(Enabled = false)]` is doing its job. That is the *point* of the demo. |

## Demo B — DynamicMethod and the benchmark

| Symptom | Fix |
|---|---|
| Ratios look wrong — reflection appears *faster* | You are in Debug. Add `-c Release`. |
| It takes 10–15 seconds | 2,000,000 iterations plus warm-up. Normal. Talk over it — walk the stack table while it runs. |
| Your numbers differ from the slide | **Expected, and say so.** Different CPU, different numbers. The *ratios* are the lesson. |
| `InvalidProgramException` escapes and kills the app | It is caught in a clean checkout. You are on a modified build — use the captured output. |
| Output wraps; tables unreadable | Terminal narrower than 80 columns. Fix before you start. |

## Demo C — Reflection.Emit

| Symptom | Fix |
|---|---|
| `PlatformNotSupportedException` | You are on a Native AOT or interpreter-only runtime. That is the *lesson* — say so and move on. Normal `dotnet run` on CoreCLR is fine. |
| The saved DLL isn't written | Check write permission on `bin/Release/net10.0/`. OneDrive sync can briefly lock files. |
| `AssemblyBuilderAccess.Save` doesn't compile | Correct — it does not exist on .NET. That is slide 22's whole point. |

## Demo D — EF Core

| Symptom | Fix |
|---|---|
| SQLite errors on startup | The in-memory database needs its connection held open; the example does that. Don't edit it live. |
| Takes a few seconds first run | EF builds and caches the model on first use. The "discover once" lesson, demonstrated. |
| Different SQL from the slide | Check your EF Core version — the slide is from **10.0.15** on SQLite. Translation is provider- and version-specific, which is itself a point on the slide. |

## Web example (if asked for it)

| Symptom | Fix |
|---|---|
| `Failed to bind to address ... 5199: address already in use` | Something else has the port. Change `UseUrls` in `10-ASPNetCoreMVC/Program.cs`, or kill the other process. |
| Hangs with no output | You ran without `--selftest`, so it is serving. That is correct — Ctrl+C, then add `-- --selftest`. |
| Firewall prompt mid-demo | It binds `127.0.0.1` only. Dismiss it. Better: run it once before the session so the prompt is already answered. |

---

# 4. Content questions that feel like problems

| Someone says | Response |
|---|---|
| "Your reflection numbers are wrong, I've measured 50x." | Both can be true — it depends on what you measure. Mine is a property read, warm, in Release, 2M iterations. `MethodInfo.Invoke` with boxed arguments on a cold path is far worse. *"What were you measuring?"* is the right reply. |
| "You can save a dynamic assembly, I've done it." | On .NET **Framework**, yes. On .NET 10 the enum has two members and the demo prints them. Saving is `PersistedAssemblyBuilder` now. |
| "`AssemblyLoadContext` sandboxes untrusted code." | It isolates type identity and versions, not privileges. There is no supported in-process sandbox in modern .NET since CAS was removed. |
| "DI uses Reflection.Emit." | It may, depending on container, version and platform. I read the engine field on .NET 10; it chooses at runtime and falls back to reflection-only under AOT. |
| "That SQL is wrong, SQL Server generates `LIKE`." | Correct — and that is the point. The *provider* chooses the translation. This is SQLite. |
| "Reflection is a security hole." | Reflection reading private state is not the hole; untrusted code running in your process is. If that is your situation, fix the boundary, not the API. |

**If they are right:** say so immediately. *"You're right, thank you — let me correct
that."* The room respects that far more than a defence.

---

# 5. Platform-specific notes

## Native AOT

Examples 05, 06 and 12 **cannot work** under Native AOT —
`RuntimeFeature.IsDynamicCodeSupported` is `false` and `Reflection.Emit` /
`DispatchProxy` throw `PlatformNotSupportedException`. None of the projects opt into AOT, so
all 13 build and run as supplied. If someone publishes AOT to try it, that failure is the
expected result and the subject of slide 16.

## Trimming

Examples 01–04 and reflection-mode JSON are at risk of having members trimmed away.
Source-generated JSON (09) is trimming-safe. Nothing here opts into trimming.

## macOS / Linux

All 14 projects are cross-platform. Only differences:

- `InvariantGlobalization=true` is set for deterministic output, so dates print as
  `MM/dd/yyyy` everywhere.
- Path separators in output will be `/`.
- Fonts differ when viewing the deck; see §2.

## Mono interpreter (iOS, some WASM)

`IsDynamicCodeCompiled` is `false`: Emit *works* but runs interpreted, so the performance
benefit disappears entirely. Worth mentioning if someone asks about mobile.

## OneDrive / synced folders

This package may live in a synced folder. Two consequences:

- Sync can briefly lock `bin/` files mid-build → a spurious `MSB3027`/`MSB3021`. Retry, or
  pause sync while presenting.
- First build after a sync can be slow. Build the day before.

---

# 6. Recovering mid-talk

| Situation | What to do |
|---|---|
| **You are 3+ minutes behind at slide 15** | Skip the demo. Use the cut list in [`37-Minute-Timeline.md`](37-Minute-Timeline.md), in order. Cut from **Part 2, never Part 3.** |
| **You are behind at slide 23** | Compress slide 23 to 30 s and drop slide 25's measurement box. Protect slides 24 and 28. |
| **You are 2 minutes ahead at slide 20** | Walk the evaluation stack again, backwards (slide 19). Or run demo C. |
| **A demo fails and you have lost 90 seconds** | Drop demo B's run — show the captured benchmark table instead. The numbers are on the slide anyway. |
| **The projector dies** | The talk works without slides for Parts 1 and 2. Draw the evaluation stack on a whiteboard for Part 3 — four columns, four instructions. Honestly, it works better by hand. |
| **Your laptop dies** | The deck exports to PDF; keep one on a phone or USB stick. The script is readable on a phone. |
| **You lose your place** | Footer on every slide: part name, duration, and `n / 28`. Glance at it and recover. |
| **A question derails you for 2 minutes** | *"Let's take that one offline so I can get you through the rest."* Then go straight to the next slide — do not re-establish context. |

---

# 7. Quick reference

```bash
# Build everything (do this the day before)
cd Code-Examples
dotnet restore Metaprogramming.slnx
dotnet build Metaprogramming.slnx -c Release

# The two essential demos
dotnet run -c Release --no-build --project 04-Plugin-Discovery/PluginDiscovery.Host
dotnet run -c Release --no-build --project 05-DynamicMethod

# Optional demos
dotnet run -c Release --no-build --project 06-ReflectionEmit
dotnet run -c Release --no-build --project 11-EntityFrameworkCore

# The web example, self-testing and exiting
dotnet run -c Release --no-build --project 10-ASPNetCoreMVC -- --selftest

# Environment sanity
dotnet --list-sdks          # expect a 10.x entry
dotnet --list-runtimes      # expect Microsoft.NETCore.App 10.x
```

**Captured output for every example:** `Code-Examples/expected-output/*.txt` — twelve files.
That is your fallback for any demo, and your evidence for any disputed claim.
