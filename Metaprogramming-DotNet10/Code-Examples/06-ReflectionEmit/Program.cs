using System.Reflection;
using System.Reflection.Emit;

namespace Metaprogramming.ReflectionEmitDemo;

/// <summary>
/// Part 3 of the session: generating whole TYPES at runtime with
/// AssemblyBuilder -> ModuleBuilder -> TypeBuilder -> MethodBuilder,
/// and persisting a generated assembly to disk on .NET 10.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. WHAT .NET 10 ACTUALLY SUPPORTS (checked at runtime, not from memory)");
        WhatIsSupported();

        Banner("2. THE BUILDER HIERARCHY - assembly, module, type, member");
        BuilderHierarchy();

        Banner("3. GENERATING A COMPLETE TYPE, STAGE BY STAGE");
        Type generated = GenerateScalerType();

        Banner("4. USING THE GENERATED TYPE");
        UseGeneratedType(generated);

        Banner("5. INSPECTING THE GENERATED TYPE WITH REFLECTION");
        InspectGeneratedType(generated);

        Banner("6. PERSISTING A GENERATED ASSEMBLY TO DISK (PersistedAssemblyBuilder)");
        PersistToDisk();

        Banner("7. LIMITS AND TRADEOFFS");
        Limits();
    }

    // =====================================================================
    // 1. The API surface, read off this runtime.
    // =====================================================================
    private static void WhatIsSupported()
    {
        Console.WriteLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        Console.WriteLine();

        // This is the single most common source of outdated advice about Reflection.Emit.
        Console.WriteLine("AssemblyBuilderAccess members that exist on this runtime:");
        foreach (string name in Enum.GetNames<AssemblyBuilderAccess>())
        {
            Console.WriteLine("  AssemblyBuilderAccess." + name);
        }

        Console.WriteLine();
        Console.WriteLine("Note what is NOT in that list: Save and RunAndSave.");
        Console.WriteLine("Those were .NET Framework members. Tutorials that call");
        Console.WriteLine("  AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave)");
        Console.WriteLine("  followed by assemblyBuilder.Save(\"Out.dll\")");
        Console.WriteLine("do not compile on modern .NET at all.");
        Console.WriteLine();

        // Confirm the runtime builder really has no Save method.
        var runBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ProbeOnly"), AssemblyBuilderAccess.Run);

        Console.WriteLine("Does the runtime AssemblyBuilder have a Save(string) method? "
            + (runBuilder.GetType().GetMethod("Save", [typeof(string)]) is not null ? "yes" : "no"));
        Console.WriteLine("Concrete runtime builder type: " + runBuilder.GetType().Name);
        Console.WriteLine("AssemblyBuilder is declared in: " + typeof(AssemblyBuilder).Assembly.GetName().Name);
        Console.WriteLine();
        Console.WriteLine("Saving IS supported on .NET 10 - through a different class:");
        Console.WriteLine("  System.Reflection.Emit.PersistedAssemblyBuilder, from the "
            + typeof(PersistedAssemblyBuilder).Assembly.GetName().Name + " assembly.");
        Console.WriteLine("  (added in .NET 9; section 6 below actually writes and reloads a DLL)");
        Console.WriteLine();
        Console.WriteLine("So the honest summary for .NET 10:");
        Console.WriteLine("  run-only, in-memory generation -> AssemblyBuilder.DefineDynamicAssembly(..., Run)");
        Console.WriteLine("  collectible/unloadable         -> ..., RunAndCollect");
        Console.WriteLine("  save a .dll to disk            -> new PersistedAssemblyBuilder(...).Save(path)");
        Console.WriteLine("  a PersistedAssemblyBuilder is for WRITING, not for running the types it defines.");
    }

    // =====================================================================
    // 2. The four builders and what each one owns.
    // =====================================================================
    private static void BuilderHierarchy()
    {
        Console.WriteLine("  AssemblyBuilder   one logical assembly, with a name and a version");
        Console.WriteLine("      |");
        Console.WriteLine("      +-- ModuleBuilder    one physical container of types (normally exactly one)");
        Console.WriteLine("              |");
        Console.WriteLine("              +-- TypeBuilder     one class, struct, interface or enum");
        Console.WriteLine("                      |");
        Console.WriteLine("                      +-- FieldBuilder        a field");
        Console.WriteLine("                      +-- ConstructorBuilder  a constructor");
        Console.WriteLine("                      +-- MethodBuilder       a method (holds an ILGenerator)");
        Console.WriteLine("                      +-- PropertyBuilder     a property (needs get/set methods)");
        Console.WriteLine();
        Console.WriteLine("The mapping is exactly the .NET metadata layout from Part 2, built in reverse:");
        Console.WriteLine("Part 2 READ Assembly -> Type -> Member. Here we WRITE the same hierarchy.");
        Console.WriteLine();
        Console.WriteLine("One rule governs the whole thing:");
        Console.WriteLine("  TypeBuilder.CreateType() 'bakes' the type. Before that call the type does not");
        Console.WriteLine("  exist and cannot be instantiated. After it, nothing more can be added.");
    }

    // =====================================================================
    // 3. Build Generated.Scaler, which implements IScaler.
    // =====================================================================
    private static Type GenerateScalerType()
    {
        Console.WriteLine("Goal - generate the equivalent of this C#, without a compiler:");
        Console.WriteLine();
        Console.WriteLine("    public class Scaler : IScaler");
        Console.WriteLine("    {");
        Console.WriteLine("        private readonly int _factor;");
        Console.WriteLine("        public Scaler(int factor) { _factor = factor; }");
        Console.WriteLine("        public int Factor => _factor;");
        Console.WriteLine("        public int Scale(int value) => _factor * value;");
        Console.WriteLine("    }");
        Console.WriteLine();

        // --- Stage 1: the assembly ---
        var assemblyName = new AssemblyName("Metaprogramming.Generated")
        {
            Version = new Version(1, 0, 0, 0)
        };

        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);          // in-memory, runnable, not savable

        Console.WriteLine("Stage 1  AssemblyBuilder.DefineDynamicAssembly(\"" + assemblyName.Name + "\", Run)");
        Console.WriteLine("         IsDynamic = " + assemblyBuilder.IsDynamic
            + ", Location = \"" + assemblyBuilder.Location + "\" (empty: it is not a file)");

        // --- Stage 2: the module ---
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        Console.WriteLine("Stage 2  DefineDynamicModule(\"MainModule\")");

        // --- Stage 3: the type ---
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            name: "Metaprogramming.Generated.Scaler",
            attr: TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            parent: typeof(object));

        // Declaring the interface is what lets the HOST cast to IScaler and call it
        // with a normal, fast interface call - no reflection at the call site.
        typeBuilder.AddInterfaceImplementation(typeof(IScaler));
        Console.WriteLine("Stage 3  DefineType(\"Metaprogramming.Generated.Scaler\") + AddInterfaceImplementation(IScaler)");

        // --- Stage 4: the field ---
        FieldBuilder factorField = typeBuilder.DefineField(
            fieldName: "_factor",
            type: typeof(int),
            attributes: FieldAttributes.Private | FieldAttributes.InitOnly);   // InitOnly == readonly
        Console.WriteLine("Stage 4  DefineField(\"_factor\", int, Private | InitOnly)");

        // --- Stage 5: the constructor ---
        ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [typeof(int)]);

        // Name the parameter so reflection and debuggers show "factor" rather than nothing.
        constructorBuilder.DefineParameter(1, ParameterAttributes.None, "factor");

        ILGenerator ctorIl = constructorBuilder.GetILGenerator();

        // Every constructor MUST chain to a base constructor. Forgetting this produces a
        // type that fails verification in confusing ways.
        ConstructorInfo objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        ctorIl.Emit(OpCodes.Ldarg_0);                   // [ this ]
        ctorIl.Emit(OpCodes.Call, objectCtor);          // [ ]          base.ctor()
        ctorIl.Emit(OpCodes.Ldarg_0);                   // [ this ]
        ctorIl.Emit(OpCodes.Ldarg_1);                   // [ this, factor ]
        ctorIl.Emit(OpCodes.Stfld, factorField);        // [ ]          this._factor = factor
        ctorIl.Emit(OpCodes.Ret);                       // [ ]

        Console.WriteLine("Stage 5  DefineConstructor(int) + IL:");
        Console.WriteLine("           ldarg.0 / call object::.ctor() / ldarg.0 / ldarg.1 / stfld _factor / ret");
        Console.WriteLine("         note: ldarg.0 is 'this' here, because this is an INSTANCE method,");
        Console.WriteLine("         so the real parameter is ldarg.1. Static methods start at ldarg.0.");

        // --- Stage 6: the Scale method (the interface implementation) ---
        MethodBuilder scaleMethod = typeBuilder.DefineMethod(
            name: "Scale",
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final
                        | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            returnType: typeof(int),
            parameterTypes: [typeof(int)]);

        // Naming the parameter is optional but shows up in reflection and debuggers.
        scaleMethod.DefineParameter(1, ParameterAttributes.None, "value");

        ILGenerator scaleIl = scaleMethod.GetILGenerator();
        scaleIl.Emit(OpCodes.Ldarg_0);                  // [ this ]
        scaleIl.Emit(OpCodes.Ldfld, factorField);       // [ _factor ]
        scaleIl.Emit(OpCodes.Ldarg_1);                  // [ _factor, value ]
        scaleIl.Emit(OpCodes.Mul);                      // [ _factor*value ]
        scaleIl.Emit(OpCodes.Ret);                      // [ ]

        // Bind our method to the interface slot explicitly. With a matching name and
        // signature the runtime would usually infer it, but being explicit is safer
        // and is required when names differ.
        typeBuilder.DefineMethodOverride(scaleMethod, typeof(IScaler).GetMethod(nameof(IScaler.Scale))!);

        Console.WriteLine("Stage 6  DefineMethod(\"Scale\", int, [int]) + IL:");
        Console.WriteLine("           ldarg.0 / ldfld _factor / ldarg.1 / mul / ret");
        Console.WriteLine("         + DefineMethodOverride(Scale, IScaler.Scale)");

        // --- Stage 7: the Factor property (getter only) ---
        MethodBuilder factorGetter = typeBuilder.DefineMethod(
            "get_Factor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            typeof(int),
            Type.EmptyTypes);

        ILGenerator getterIl = factorGetter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);                 // [ this ]
        getterIl.Emit(OpCodes.Ldfld, factorField);      // [ _factor ]
        getterIl.Emit(OpCodes.Ret);                     // [ ]

        // A property is metadata that POINTS AT accessor methods. The methods do the work.
        PropertyBuilder factorProperty = typeBuilder.DefineProperty(
            "Factor", PropertyAttributes.HasDefault, typeof(int), null);
        factorProperty.SetGetMethod(factorGetter);

        Console.WriteLine("Stage 7  DefineMethod(\"get_Factor\") + DefineProperty(\"Factor\").SetGetMethod(...)");
        Console.WriteLine("         a property in metadata is just a name bound to accessor methods.");

        // --- Stage 8: bake it ---
        Type created = typeBuilder.CreateType();

        Console.WriteLine("Stage 8  CreateType()  ->  " + created.FullName);
        Console.WriteLine();
        Console.WriteLine("Before CreateType() the type was a description. After it, the runtime has a real");
        Console.WriteLine("Type with metadata and IL, indistinguishable from a compiled one to its callers.");

        return created;
    }

    // =====================================================================
    // 4. Use it: reflectively, then through the interface.
    // =====================================================================
    private static void UseGeneratedType(Type generated)
    {
        // The host cannot write 'new Scaler(7)' - that type did not exist at compile time.
        object? instance = Activator.CreateInstance(generated, 7);
        Console.WriteLine("Activator.CreateInstance(generated, 7) -> " + instance);

        // (a) Call it reflectively.
        MethodInfo scale = generated.GetMethod("Scale")!;
        Console.WriteLine();
        Console.WriteLine("Reflective invocation:");
        Console.WriteLine("  Scale(6)  = " + scale.Invoke(instance, [6]));
        Console.WriteLine("  Scale(10) = " + scale.Invoke(instance, [10]));

        // (b) Read the generated property reflectively.
        PropertyInfo factor = generated.GetProperty("Factor")!;
        Console.WriteLine("  Factor    = " + factor.GetValue(instance));

        // (c) The much better way: the generated type implements an interface the host
        //     DOES know, so cast once and then make ordinary interface calls.
        var scaler = (IScaler)instance!;
        Console.WriteLine();
        Console.WriteLine("Through the IScaler interface - no reflection at the call site:");
        Console.WriteLine("  scaler.Scale(6)  = " + scaler.Scale(6));
        Console.WriteLine("  scaler.Scale(10) = " + scaler.Scale(10));

        // (d) Several instances, each with its own field value.
        Console.WriteLine();
        Console.WriteLine("Each instance keeps its own _factor, as a real field should:");
        foreach (int f in new[] { 2, 3, 100 })
        {
            var s = (IScaler)Activator.CreateInstance(generated, f)!;
            Console.WriteLine("  new Scaler(" + f + ").Scale(5) = " + s.Scale(5));
        }

        Console.WriteLine();
        Console.WriteLine("This interface trick is the whole design pattern behind runtime type generation:");
        Console.WriteLine("generate a type that implements a KNOWN contract, then talk to it through that");
        Console.WriteLine("contract. Reflection is used once, at creation; never on the hot path.");
    }

    // =====================================================================
    // 5. The generated type is fully described in metadata.
    // =====================================================================
    private static void InspectGeneratedType(Type generated)
    {
        Console.WriteLine("Name            : " + generated.FullName);
        Console.WriteLine("Assembly        : " + generated.Assembly.GetName().Name + " (IsDynamic=" + generated.Assembly.IsDynamic + ")");
        Console.WriteLine("Module          : " + generated.Module.Name);
        Console.WriteLine("BaseType        : " + generated.BaseType?.Name);
        Console.WriteLine("Interfaces      : " + string.Join(", ", generated.GetInterfaces().Select(i => i.Name)));
        Console.WriteLine("IsSealed        : " + generated.IsSealed);

        Console.WriteLine();
        Console.WriteLine("Constructors:");
        foreach (ConstructorInfo c in generated.GetConstructors())
        {
            Console.WriteLine("  .ctor(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Methods (declared only):");
        foreach (MethodInfo m in generated.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Console.WriteLine("  " + m.ReturnType.Name + " " + m.Name
                + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")"
                + (m.IsVirtual ? "  [virtual]" : ""));
        }

        Console.WriteLine();
        Console.WriteLine("Properties:");
        foreach (PropertyInfo p in generated.GetProperties())
        {
            Console.WriteLine("  " + p.PropertyType.Name + " " + p.Name + " { " + (p.CanRead ? "get;" : "") + (p.CanWrite ? " set;" : "") + " }");
        }

        Console.WriteLine();
        Console.WriteLine("Fields (including private):");
        foreach (FieldInfo f in generated.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine("  " + (f.IsPrivate ? "private " : "") + (f.IsInitOnly ? "readonly " : "")
                + f.FieldType.Name + " " + f.Name);
        }

        Console.WriteLine();
        Console.WriteLine("Part 2's reflection code works on this type unchanged. Generated metadata and");
        Console.WriteLine("compiled metadata are the same kind of thing.");
    }

    // =====================================================================
    // 6. Writing a real DLL to disk - verified, not claimed.
    // =====================================================================
    private static void PersistToDisk()
    {
        // PersistedAssemblyBuilder is a SEPARATE class from AssemblyBuilder. It builds
        // metadata for a file; it does not produce types you can run in this process.
        // The second argument is the "core assembly" its type references resolve against.
        var persisted = new PersistedAssemblyBuilder(
            new AssemblyName("Metaprogramming.Saved") { Version = new Version(1, 0, 0, 0) },
            coreAssembly: typeof(object).Assembly);

        ModuleBuilder module = persisted.DefineDynamicModule("Metaprogramming.Saved.dll");

        TypeBuilder typeBuilder = module.DefineType(
            "Metaprogramming.Saved.MathHelpers",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);  // static class

        // public static int Add(int a, int b) => a + b;
        MethodBuilder add = typeBuilder.DefineMethod(
            "Add", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int), typeof(int)]);
        ILGenerator addIl = add.GetILGenerator();
        addIl.Emit(OpCodes.Ldarg_0);
        addIl.Emit(OpCodes.Ldarg_1);
        addIl.Emit(OpCodes.Add);
        addIl.Emit(OpCodes.Ret);

        // public static int Square(int a) => a * a;
        MethodBuilder square = typeBuilder.DefineMethod(
            "Square", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
        ILGenerator squareIl = square.GetILGenerator();
        squareIl.Emit(OpCodes.Ldarg_0);
        squareIl.Emit(OpCodes.Ldarg_0);
        squareIl.Emit(OpCodes.Mul);
        squareIl.Emit(OpCodes.Ret);

        typeBuilder.CreateType();

        // Write it next to this program's own binaries.
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Metaprogramming.Saved.dll");
        persisted.Save(outputPath);

        var info = new FileInfo(outputPath);
        Console.WriteLine("Wrote a real assembly to disk:");
        Console.WriteLine("  path  : " + outputPath);
        Console.WriteLine("  exists: " + info.Exists);
        Console.WriteLine("  bytes : " + info.Length);

        // Prove it is a usable assembly by loading the FILE back and calling into it.
        Assembly reloaded = Assembly.LoadFrom(outputPath);
        Type mathHelpers = reloaded.GetType("Metaprogramming.Saved.MathHelpers")!;

        Console.WriteLine();
        Console.WriteLine("Loaded that file back in and invoked it:");
        Console.WriteLine("  assembly  : " + reloaded.GetName().Name + " v" + reloaded.GetName().Version);
        Console.WriteLine("  IsDynamic : " + reloaded.IsDynamic + "  (false - it came from a file)");
        Console.WriteLine("  Add(20,22)= " + mathHelpers.GetMethod("Add")!.Invoke(null, [20, 22]));
        Console.WriteLine("  Square(9) = " + mathHelpers.GetMethod("Square")!.Invoke(null, [9]));

        Console.WriteLine();
        Console.WriteLine("Why persistence is worth knowing about:");
        Console.WriteLine("  - you can open the result in ILSpy or ildasm and READ the IL you emitted,");
        Console.WriteLine("    which turns IL debugging from guesswork into inspection;");
        Console.WriteLine("  - you can run ILVerify over it to catch invalid IL properly;");
        Console.WriteLine("  - build-time tools can emit an assembly for later consumption.");
        Console.WriteLine();
        Console.WriteLine("Two cautions:");
        Console.WriteLine("  - PersistedAssemblyBuilder is for WRITING. Do not expect to execute its types");
        Console.WriteLine("    directly; save, then load the file (as above), or use the Run builder instead.");
        Console.WriteLine("  - Save() needs the IL to be complete. CreateType() on every TypeBuilder first.");
    }

    // =====================================================================
    // 7. Tradeoffs.
    // =====================================================================
    private static void Limits()
    {
        Console.WriteLine("Who really does this in production:");
        Console.WriteLine("  - Castle DynamicProxy / Moq / NSubstitute: generate proxy and mock TYPES");
        Console.WriteLine("  - EF Core: lazy-loading proxies");
        Console.WriteLine("  - Serializers and mappers: generated read/write members");
        Console.WriteLine("  - Regex with RegexOptions.Compiled: emits a matcher");
        Console.WriteLine();
        Console.WriteLine("Costs you are accepting:");
        Console.WriteLine("  - Native AOT: unavailable. RuntimeFeature.IsDynamicCodeSupported is false and");
        Console.WriteLine("    these APIs throw PlatformNotSupportedException.");
        Console.WriteLine("  - Trimming: the trimmer cannot see types that do not exist yet.");
        Console.WriteLine("  - First-use latency: building plus JIT-ing costs real milliseconds. Cache the");
        Console.WriteLine("    generated Type; never regenerate per request.");
        Console.WriteLine("  - Run-only assemblies are NOT collectible unless you use RunAndCollect, and a");
        Console.WriteLine("    cached Type keeps the whole context alive (see example 04's unload section).");
        Console.WriteLine("  - Verbosity: the 60-odd lines above replace 6 lines of C#.");
        Console.WriteLine();
        Console.WriteLine("Before writing any of this, check in order:");
        Console.WriteLine("  1. Generics or a cached delegate.");
        Console.WriteLine("  2. DispatchProxy, if you only need to intercept an INTERFACE (example 12).");
        Console.WriteLine("  3. Expression trees, if you need a compiled FUNCTION rather than a TYPE (example 08).");
        Console.WriteLine("  4. A Roslyn source generator, if the shape is known at build time.");
        Console.WriteLine("  5. TypeBuilder - only when you genuinely need a new TYPE at run time.");
    }

    private static void Banner(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }
}

/// <summary>
/// The contract the generated type will implement. Because the host knows this interface
/// at compile time, it can call the generated type without reflection.
/// </summary>
public interface IScaler
{
    int Scale(int value);
}
