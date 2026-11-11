# Captured output

The complete console output of all twelve examples, captured by actually running them.

| | |
|---|---|
| Captured on | 2026-11-11 |
| Runtime | .NET 10.0.11 (SDK 10.0.400) |
| OS | Windows 11 x64 |
| Configuration | **Release** |
| Result | **12 / 12 ran to completion, exit code 0** |

```bash
# how these were produced
dotnet build Metaprogramming.slnx -c Release
dotnet run -c Release --no-build --project <project> > expected-output/<name>.txt 2>&1

# the web example self-tests over HTTP and exits
dotnet run -c Release --no-build --project 10-ASPNetCoreMVC -- --selftest
```

## Why these files exist

1. **Compare your run against a known-good one.** `diff` your output against these.
2. **A fallback for a live demo.** If a demo misbehaves on stage, show the transcript
   instead. See [`../../Presenter-Guide/Live-Demo-Guide.md`](../../Presenter-Guide/Live-Demo-Guide.md).
3. **Evidence.** Every console output quoted in the study guide and on the slides came from
   these files, not from expectation.

## What will differ on your machine

Most of this output is **deterministic** — the examples use fixed dates, fixed seeds,
`InvariantGlobalization`, and no randomness. Four kinds of line will differ, by design:

| What | Where | Why |
|---|---|---|
| **Timings in milliseconds** | `05-DynamicMethod` §7, `09-Serialization` §3 | Your CPU. The **ratios** are the lesson, not the absolute values. Debug vs Release matters a lot. |
| **The garbage number from invalid IL** | `05-DynamicMethod` §8, Case B | It is a managed reference reinterpreted as an integer. The output says so itself: *"it is not stable across runs"*. |
| **A random reference string** | `02-Reflection` §9(d) — `LedgerEntry LE-xxxxxx` | A `Guid` fragment. |
| **Absolute file paths** | `04`, `06`, `10` | Your checkout location. |

Everything else — every discovered type, every attribute, every exception message, every IL
listing, every generated SQL statement, the `Byte-for-byte identical: True` line, the DLL
byte count — should match exactly.

## Sanity check: the claims these files support

If your run agrees with these, the session's measured claims hold on your hardware too:

| Claim | Where to look |
|---|---|
| Reflection is ≈6–8× a direct property read, and that is tens of nanoseconds | `05-DynamicMethod` §7 |
| **A cached delegate is as fast as emitted IL** | `05` §7, rows 4 and 5 — within noise of each other |
| Boxing/signature shape costs more than reflection-vs-emit | `05` §7, rows 5 and 6 |
| A missing `ret` throws on **first call**; a type mismatch throws **nothing** | `05` §8 |
| `AssemblyBuilderAccess` has only `Run` and `RunAndCollect` on .NET 10 | `06-ReflectionEmit` §1 |
| `PersistedAssemblyBuilder` writes a real, reloadable DLL | `06` §6 |
| The DI engine is chosen at runtime (`DynamicServiceProviderEngine` here) | `07-DependencyInjection` §8 |
| Reflection-mode and source-generated JSON are byte-identical | `09-Serialization` §6 |
| Varying `JsonSerializerOptions` per call is the expensive mistake | `09` §3 |
| MVC discovers controllers at startup, serves from a cached table | `10-ASPNetCoreMVC` §1–4 |
| EF Core renders `string.Contains` as `instr(...)` on SQLite, not `LIKE` | `11-EntityFrameworkCore` §5B |
| A collectible `AssemblyLoadContext` really does unload | `04-Plugin-Discovery` §4 |

## Line counts

| File | Lines |
|---|---|
| `01-Basics.txt` | 98 |
| `02-Reflection.txt` | 164 |
| `03-Attributes.txt` | 117 |
| `04-Plugin-Discovery.txt` | 105 |
| `05-DynamicMethod.txt` | 238 |
| `06-ReflectionEmit.txt` | 182 |
| `07-DependencyInjection.txt` | 176 |
| `08-ExpressionTrees.txt` | 197 |
| `09-Serialization.txt` | 265 |
| `10-ASPNetCoreMVC.txt` | 129 |
| `11-EntityFrameworkCore.txt` | 327 |
| `12-DynamicProxies.txt` | 248 |
| **Total** | **2,246** |
