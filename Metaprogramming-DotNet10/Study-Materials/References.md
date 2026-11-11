# References

Official Microsoft documentation, grouped by session part.

**Link verification.** Every URL below was checked with an HTTP request while this
package was produced (2026-11-10): all returned HTTP 200. Nothing here is a guessed
or reconstructed URL. Anything that could not be verified would be labelled
**UNVERIFIED** — and no entry in this file carries that label.

Documentation moves. If a link 404s later, search `learn.microsoft.com` for the API
name; the API reference pages (`/dotnet/api/...`) are the most stable.

---

## Foundations

| Topic | Link |
|---|---|
| What's new in .NET 10 | <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview> |
| Managed code and the CLR | <https://learn.microsoft.com/en-us/dotnet/standard/managed-code> |
| Assemblies in .NET | <https://learn.microsoft.com/en-us/dotnet/standard/assembly/> |

---

## Part 1 — Understanding Metaprogramming

| Topic | Link |
|---|---|
| Reflection and attributes (C# guide) | <https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/> |
| Source generators overview (compile-time metaprogramming) | <https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview> |
| Writing custom attributes | <https://learn.microsoft.com/en-us/dotnet/standard/attributes/writing-custom-attributes> |

---

## Part 2 — Runtime Introspection

| Topic | Link |
|---|---|
| Reflection in .NET (concepts) | <https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/reflection> |
| `System.Reflection` namespace | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection> |
| `Type` | <https://learn.microsoft.com/en-us/dotnet/api/system.type> |
| `Assembly` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly> |
| `MethodInfo` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo> |
| `PropertyInfo` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.propertyinfo> |
| `ConstructorInfo` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.constructorinfo> |
| `FieldInfo` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.fieldinfo> |
| `BindingFlags` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.bindingflags> |
| `Activator.CreateInstance` | <https://learn.microsoft.com/en-us/dotnet/api/system.activator.createinstance> |
| `AttributeUsageAttribute` | <https://learn.microsoft.com/en-us/dotnet/api/system.attributeusageattribute> |
| `AttributeTargets` | <https://learn.microsoft.com/en-us/dotnet/api/system.attributetargets> |
| `CustomAttributeData` (read attributes without constructing them) | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.customattributedata> |
| `MetadataLoadContext` (inspect assemblies without executing them) | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadataloadcontext> |

---

## Part 3 — Runtime Code Generation

| Topic | Link |
|---|---|
| Emitting dynamic methods and assemblies | <https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/emitting-dynamic-methods-and-assemblies> |
| `System.Reflection.Emit` namespace | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit> |
| `DynamicMethod` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.dynamicmethod> |
| `ILGenerator` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator> |
| `OpCodes` (every IL instruction) | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes> |
| `AssemblyBuilder` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder> |
| `AssemblyBuilderAccess` — **note it has only `Run` and `RunAndCollect`** | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilderaccess> |
| `PersistedAssemblyBuilder` — **how you save a generated assembly on modern .NET** | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.persistedassemblybuilder> |
| `TypeBuilder` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.typebuilder> |
| `MethodBuilder` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.methodbuilder> |
| `RuntimeFeature.IsDynamicCodeSupported` | <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature.isdynamiccodesupported> |

> **The single most important documentation note for this part.** Many tutorials show
> `AssemblyBuilderAccess.RunAndSave` followed by `assemblyBuilder.Save("Out.dll")`.
> Those members are **.NET Framework only** and do not exist on .NET Core / .NET 5+.
> The modern replacement is `PersistedAssemblyBuilder`. Example 06 in this package
> prints the live enum members so you can confirm this on your own machine.

---

## Part 4 — Real-World Applications

### Plugins and assembly loading

| Topic | Link |
|---|---|
| Understanding `AssemblyLoadContext` | <https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext> |
| `AssemblyLoadContext` API | <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext> |
| Assembly unloadability (and what prevents it) | <https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability> |
| Tutorial: create an app with plugin support | <https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support> |

### Dependency injection

| Topic | Link |
|---|---|
| Dependency injection in .NET | <https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection> |
| DI guidelines (including captive dependencies) | <https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines> |
| `ActivatorUtilities` | <https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities> |

### ASP.NET Core MVC

| Topic | Link |
|---|---|
| Routing to controller actions | <https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing> |
| Application parts | <https://learn.microsoft.com/en-us/aspnet/core/mvc/advanced/app-parts> |

### Entity Framework Core

| Topic | Link |
|---|---|
| EF Core documentation | <https://learn.microsoft.com/en-us/ef/core/> |
| Creating and configuring a model | <https://learn.microsoft.com/en-us/ef/core/modeling/> |
| How queries work (LINQ to SQL translation) | <https://learn.microsoft.com/en-us/ef/core/querying/how-query-works> |
| Client vs server evaluation (why translation fails) | <https://learn.microsoft.com/en-us/ef/core/querying/client-eval> |

### Serialization

| Topic | Link |
|---|---|
| `System.Text.Json` overview | <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview> |
| Source generation in `System.Text.Json` | <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation> |

### Expression trees

| Topic | Link |
|---|---|
| Expression trees (C# guide) | <https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/> |
| `Expression<TDelegate>` | <https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1> |
| `ExpressionVisitor` | <https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor> |
| `IQueryable` | <https://learn.microsoft.com/en-us/dotnet/api/system.linq.iqueryable> |

### Dynamic proxies

| Topic | Link |
|---|---|
| `DispatchProxy` | <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy> |

---

## Native AOT and trimming — the limits of all of the above

This is the section to read before shipping anything built on runtime metaprogramming.

| Topic | Link |
|---|---|
| Native AOT deployment (see the **"Limitations of Native AOT deployment"** section) | <https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment> |
| Native AOT diagnostics support | <https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/diagnostics> |
| Trim self-contained deployments | <https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained> |
| Known trimming incompatibilities | <https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities> |
| Prepare libraries for trimming | <https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming> |
| Fixing trim warnings | <https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings> |
| `DynamicallyAccessedMembersAttribute` (tell the trimmer what reflection needs) | <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute> |

---

## Tools worth knowing

These are not Microsoft documentation pages, so they are listed separately and
described rather than linked, to keep every URL in this file verified:

- **ILSpy** — open-source .NET decompiler. Open any assembly and read its IL or
  decompiled C#. The fastest way to learn what IL your C# actually produces, and the
  way to inspect an assembly saved with `PersistedAssemblyBuilder`.
  Available from the ILSpy project on GitHub and the Visual Studio Marketplace.
- **ildasm / ilasm** — the IL disassembler and assembler shipped with the .NET SDK
  tooling and the Windows SDK.
- **ILVerify** — a .NET tool (`dotnet tool install -g dotnet-ilverify`) that validates
  IL in an assembly. Pair it with `PersistedAssemblyBuilder` to check emitted IL
  properly instead of waiting for `InvalidProgramException` at run time.
- **BenchmarkDotNet** — the correct way to measure any of the performance claims in
  this package. The `Stopwatch` loops in examples 05 and 09 are good enough to show an
  order of magnitude and no more.
- **SharpLab** (sharplab.io) — paste C#, see the IL or the lowered C#. Useful live
  during a talk to show what the compiler emits for a lambda or a record.

---

## Suggested reading order

1. Reflection in .NET (concepts) — the whole of Part 2 in one page.
2. Writing custom attributes — short, and it anchors the "metadata" idea.
3. Expression trees (C# guide) — read this **before** `Reflection.Emit`; it is the
   better tool for most problems people reach for `Emit` to solve.
4. Emitting dynamic methods and assemblies — then the `PersistedAssemblyBuilder`
   reference page, because the conceptual page predates it.
5. Understanding `AssemblyLoadContext`, then assembly unloadability.
6. Native AOT limitations — so you know which of the above you can actually ship.
