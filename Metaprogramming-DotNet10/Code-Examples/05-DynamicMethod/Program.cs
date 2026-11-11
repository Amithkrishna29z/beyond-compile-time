using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Metaprogramming.DynamicMethodDemo;

/// <summary>
/// Part 3 of the session: lightweight runtime code generation with DynamicMethod.
/// Every emitted instruction is printed together with the state of the evaluation stack,
/// so the audience can follow what the IL actually does.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("0. CAN THIS RUNTIME GENERATE CODE AT ALL?");
        RuntimeCapabilities();

        Banner("1. ldarg.0 / ldarg.1 / add / ret - the canonical Add method");
        EmitAdd();

        Banner("2. mul - the same shape, one opcode different");
        EmitMultiply();

        Banner("3. ldc.i4 - pushing CONSTANTS onto the stack");
        EmitAffine();

        Banner("4. call - invoking an existing method from emitted IL");
        EmitCallIntoMathMax();

        Banner("5. newobj - constructing an object from emitted IL");
        EmitNewObj();

        Banner("6. A USEFUL ONE - a fast property getter");
        EmitFastPropertyGetter();

        Banner("7. PERFORMANCE - direct vs reflection vs cached delegate vs emitted IL");
        Benchmark();

        Banner("8. WHAT HAPPENS WHEN THE IL IS WRONG");
        InvalidIl();

        Banner("9. DynamicMethod vs MethodBuilder, AND THE LIMITS");
        Limits();
    }

    // =====================================================================
    // 0. Runtime code generation is a capability, not a guarantee.
    // =====================================================================
    private static void RuntimeCapabilities()
    {
        Console.WriteLine("RuntimeInformation.FrameworkDescription : "
            + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        Console.WriteLine("RuntimeFeature.IsDynamicCodeSupported   : " + RuntimeFeature.IsDynamicCodeSupported);
        Console.WriteLine("RuntimeFeature.IsDynamicCodeCompiled    : " + RuntimeFeature.IsDynamicCodeCompiled);

        Console.WriteLine();
        Console.WriteLine("What these two flags mean:");
        Console.WriteLine("  IsDynamicCodeSupported = can I create code at runtime at all?");
        Console.WriteLine("    false under Native AOT. Reflection.Emit throws PlatformNotSupportedException there.");
        Console.WriteLine("  IsDynamicCodeCompiled  = will that code be JIT-compiled to machine code?");
        Console.WriteLine("    false on Mono interpreter builds (e.g. iOS): it works, but runs interpreted.");
        Console.WriteLine();
        Console.WriteLine("Library code that emits IL should check IsDynamicCodeSupported and keep a");
        Console.WriteLine("reflection-based fallback. That is exactly what System.Text.Json and the");
        Console.WriteLine("DI container do internally.");
    }

    // =====================================================================
    // 1. Add. Four instructions, explained one at a time.
    // =====================================================================
    private static void EmitAdd()
    {
        // A DynamicMethod is a method with no declaring type. It is not attached to any
        // assembly you can save; it exists only for this process.
        var method = new DynamicMethod(
            name: "Add",                                 // for stack traces and debugging only
            returnType: typeof(int),
            parameterTypes: [typeof(int), typeof(int)]);

        ILGenerator il = method.GetILGenerator();

        Console.WriteLine("Signature: int Add(int a, int b)");
        Console.WriteLine();
        Console.WriteLine("  IL                 | evaluation stack after the instruction (top on the right)");
        Console.WriteLine("  -------------------+-------------------------------------------------------");

        il.Emit(OpCodes.Ldarg_0);
        Console.WriteLine("  ldarg.0            | [ a ]                 push argument 0");

        il.Emit(OpCodes.Ldarg_1);
        Console.WriteLine("  ldarg.1            | [ a, b ]              push argument 1");

        il.Emit(OpCodes.Add);
        Console.WriteLine("  add                | [ a+b ]               pop two, push their sum");

        il.Emit(OpCodes.Ret);
        Console.WriteLine("  ret                | [ ]                   pop the return value and return");

        // CreateDelegate hands the IL to the JIT and gives back something directly callable.
        // From here on the cost is a normal delegate invocation - no reflection per call.
        var add = method.CreateDelegate<Func<int, int, int>>();

        Console.WriteLine();
        Console.WriteLine("add(2, 3)    = " + add(2, 3));
        Console.WriteLine("add(40, 2)   = " + add(40, 2));
        Console.WriteLine("add(-7, 7)   = " + add(-7, 7));

        Console.WriteLine();
        Console.WriteLine("Two rules the verifier enforces and that explain most emit bugs:");
        Console.WriteLine("  1. The stack must be empty when the method returns (ret pops exactly the");
        Console.WriteLine("     return value, nothing more).");
        Console.WriteLine("  2. Operand types must match the opcode. 'add' on an int and a string is invalid IL.");
        Console.WriteLine();
        Console.WriteLine("Note on argument numbering: for a STATIC method ldarg.0 is the first real argument.");
        Console.WriteLine("For an instance method ldarg.0 is 'this' and the first argument is ldarg.1.");
        Console.WriteLine("A DynamicMethod declared like this one is static.");
    }

    // =====================================================================
    // 2. Multiply - showing that the operator is a single opcode swap.
    // =====================================================================
    private static void EmitMultiply()
    {
        var method = new DynamicMethod("Multiply", typeof(int), [typeof(int), typeof(int)]);
        ILGenerator il = method.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);   // [ a ]
        il.Emit(OpCodes.Ldarg_1);   // [ a, b ]
        il.Emit(OpCodes.Mul);       // [ a*b ]
        il.Emit(OpCodes.Ret);       // [ ]

        var multiply = method.CreateDelegate<Func<int, int, int>>();

        Console.WriteLine("Identical IL to Add, with 'mul' instead of 'add':");
        Console.WriteLine("  ldarg.0 / ldarg.1 / mul / ret");
        Console.WriteLine();
        Console.WriteLine("multiply(6, 7)  = " + multiply(6, 7));
        Console.WriteLine("multiply(12, 12)= " + multiply(12, 12));

        // The same pattern generalised: pick the opcode from data.
        Console.WriteLine();
        Console.WriteLine("Because the opcode is just data, one factory can build all four operators:");
        foreach (string op in new[] { "+", "-", "*", "/" })
        {
            Func<int, int, int> generated = ArithmeticFactory.Build(op);
            Console.WriteLine("  Build(\"" + op + "\")(84, 2) = " + generated(84, 2));
        }

        Console.WriteLine();
        Console.WriteLine("That is the real shape of runtime code generation: a small interpreter of");
        Console.WriteLine("YOUR data (here, an operator string) turned into machine code once, then reused.");
    }

    // =====================================================================
    // 3. Constants: ldc.i4 and its compact forms.
    // =====================================================================
    private static void EmitAffine()
    {
        // Target: int f(int x) => (x * 2) + 10;
        var method = new DynamicMethod("Affine", typeof(int), [typeof(int)]);
        ILGenerator il = method.GetILGenerator();

        Console.WriteLine("Target: int f(int x) => (x * 2) + 10;");
        Console.WriteLine();
        Console.WriteLine("  IL                 | stack after");
        Console.WriteLine("  -------------------+----------------------");

        il.Emit(OpCodes.Ldarg_0);
        Console.WriteLine("  ldarg.0            | [ x ]");

        // Ldc_I4_2 is a one-byte instruction for the constant 2.
        il.Emit(OpCodes.Ldc_I4_2);
        Console.WriteLine("  ldc.i4.2           | [ x, 2 ]        one-byte form for small constants");

        il.Emit(OpCodes.Mul);
        Console.WriteLine("  mul                | [ x*2 ]");

        // Ldc_I4_S takes a one-byte operand (-128..127). Emit takes an sbyte overload.
        il.Emit(OpCodes.Ldc_I4_S, (sbyte)10);
        Console.WriteLine("  ldc.i4.s 10        | [ x*2, 10 ]     short form, operand fits in a signed byte");

        il.Emit(OpCodes.Add);
        Console.WriteLine("  add                | [ x*2+10 ]");

        il.Emit(OpCodes.Ret);
        Console.WriteLine("  ret                | [ ]");

        var affine = method.CreateDelegate<Func<int, int>>();

        Console.WriteLine();
        foreach (int x in new[] { 0, 1, 16, 100 })
        {
            Console.WriteLine("  f(" + x + ") = " + affine(x));
        }

        Console.WriteLine();
        Console.WriteLine("The ldc.i4 family, smallest encoding first:");
        Console.WriteLine("  ldc.i4.0 .. ldc.i4.8   1 byte, for 0-8");
        Console.WriteLine("  ldc.i4.m1              1 byte, for -1");
        Console.WriteLine("  ldc.i4.s <sbyte>       2 bytes, for -128..127");
        Console.WriteLine("  ldc.i4 <int32>         5 bytes, any 32-bit value");
        Console.WriteLine("All four push the same thing; they differ only in how many bytes the IL takes.");
    }

    // =====================================================================
    // 4. call - reaching an existing method from generated code.
    // =====================================================================
    private static void EmitCallIntoMathMax()
    {
        // Target: int f(int a, int b) => Math.Max(a, b) * 10;
        MethodInfo mathMax = typeof(Math).GetMethod(nameof(Math.Max), [typeof(int), typeof(int)])!;

        var method = new DynamicMethod("MaxTimesTen", typeof(int), [typeof(int), typeof(int)]);
        ILGenerator il = method.GetILGenerator();

        Console.WriteLine("Target: int f(int a, int b) => Math.Max(a, b) * 10;");
        Console.WriteLine();
        Console.WriteLine("  IL                              | stack after");
        Console.WriteLine("  --------------------------------+----------------------");

        il.Emit(OpCodes.Ldarg_0);
        Console.WriteLine("  ldarg.0                         | [ a ]");

        il.Emit(OpCodes.Ldarg_1);
        Console.WriteLine("  ldarg.1                         | [ a, b ]");

        // call pops the arguments in order and pushes the return value (if any).
        il.Emit(OpCodes.Call, mathMax);
        Console.WriteLine("  call int32 Math::Max(int32,int32)| [ max ]        popped 2 args, pushed 1 result");

        il.Emit(OpCodes.Ldc_I4_S, (sbyte)10);
        Console.WriteLine("  ldc.i4.s 10                     | [ max, 10 ]");

        il.Emit(OpCodes.Mul);
        Console.WriteLine("  mul                             | [ max*10 ]");

        il.Emit(OpCodes.Ret);
        Console.WriteLine("  ret                             | [ ]");

        var maxTimesTen = method.CreateDelegate<Func<int, int, int>>();

        Console.WriteLine();
        Console.WriteLine("  f(3, 9)  = " + maxTimesTen(3, 9));
        Console.WriteLine("  f(42, 1) = " + maxTimesTen(42, 1));

        Console.WriteLine();
        Console.WriteLine("call vs callvirt:");
        Console.WriteLine("  call     - the target is decided now (static methods, non-virtual instance methods).");
        Console.WriteLine("  callvirt - the target is looked up from the object's real type at run time, and it");
        Console.WriteLine("             also null-checks the receiver. Used for virtual/interface methods.");
    }

    // =====================================================================
    // 5. newobj - allocate and construct.
    // =====================================================================
    private static void EmitNewObj()
    {
        // Target: object f(int x, int y) => new Point(x, y);
        ConstructorInfo pointCtor = typeof(Point).GetConstructor([typeof(int), typeof(int)])!;

        var method = new DynamicMethod("MakePoint", typeof(object), [typeof(int), typeof(int)]);
        ILGenerator il = method.GetILGenerator();

        Console.WriteLine("Target: object f(int x, int y) => new Point(x, y);");
        Console.WriteLine();
        Console.WriteLine("  IL                          | stack after");
        Console.WriteLine("  ----------------------------+----------------------");

        il.Emit(OpCodes.Ldarg_0);
        Console.WriteLine("  ldarg.0                     | [ x ]");

        il.Emit(OpCodes.Ldarg_1);
        Console.WriteLine("  ldarg.1                     | [ x, y ]");

        // newobj allocates the object, runs the constructor with the popped arguments,
        // and pushes the reference. It is ONE instruction for allocate + initialise.
        il.Emit(OpCodes.Newobj, pointCtor);
        Console.WriteLine("  newobj Point::.ctor(int,int)| [ Point ]      popped 2 args, pushed the new reference");

        il.Emit(OpCodes.Ret);
        Console.WriteLine("  ret                         | [ ]");

        var makePoint = method.CreateDelegate<Func<int, int, object>>();

        Console.WriteLine();
        Console.WriteLine("  f(3, 4) = " + makePoint(3, 4));
        Console.WriteLine("  f(0, 0) = " + makePoint(0, 0));

        Console.WriteLine();
        Console.WriteLine("This single instruction is what a hand-written factory, a DI container and");
        Console.WriteLine("Activator.CreateInstance all ultimately have to perform.");
        Console.WriteLine("Emitting it once and caching the delegate is how containers avoid paying");
        Console.WriteLine("reflection costs on every resolve.");
    }

    // =====================================================================
    // 6. The genuinely useful pattern: a generated accessor.
    // =====================================================================
    private static void EmitFastPropertyGetter()
    {
        PropertyInfo property = typeof(Employee).GetProperty(nameof(Employee.Salary))!;
        MethodInfo getter = property.GetGetMethod()!;

        // Target: object f(object instance) => ((Employee)instance).Salary;  (boxed)
        var method = new DynamicMethod("GetSalary", typeof(object), [typeof(object)]);
        ILGenerator il = method.GetILGenerator();

        Console.WriteLine("Target: object f(object o) => (object)((Employee)o).Salary;");
        Console.WriteLine();

        il.Emit(OpCodes.Ldarg_0);                       // [ o ]
        Console.WriteLine("  ldarg.0                      | [ o ]            the untyped instance");

        il.Emit(OpCodes.Castclass, typeof(Employee));   // [ (Employee)o ]
        Console.WriteLine("  castclass Employee           | [ Employee ]     checked cast, throws if wrong type");

        il.Emit(OpCodes.Callvirt, getter);              // [ decimal ]
        Console.WriteLine("  callvirt get_Salary()        | [ decimal ]      run the property getter");

        il.Emit(OpCodes.Box, typeof(decimal));          // [ object ]
        Console.WriteLine("  box decimal                  | [ object ]       value type -> heap object");

        il.Emit(OpCodes.Ret);
        Console.WriteLine("  ret                          | [ ]");

        var fastGetter = method.CreateDelegate<Func<object, object>>();

        var employee = new Employee { Name = "Ravi", Salary = 94_500m };

        Console.WriteLine();
        Console.WriteLine("  emitted getter  -> " + fastGetter(employee));
        Console.WriteLine("  PropertyInfo    -> " + property.GetValue(employee));
        Console.WriteLine("  direct property -> " + employee.Salary);
        Console.WriteLine();
        Console.WriteLine("All three read the same value. The difference is cost per call, measured next.");
    }

    // =====================================================================
    // 7. Numbers, measured on this machine right now.
    // =====================================================================
    private static void Benchmark()
    {
        const int iterations = 2_000_000;
        var employee = new Employee { Name = "Ravi", Salary = 94_500m };

        PropertyInfo property = typeof(Employee).GetProperty(nameof(Employee.Salary))!;
        MethodInfo getter = property.GetGetMethod()!;

        // (d) emitted IL, untyped in and out (object -> object). This is what a serializer
        //     or mapper needs, because it does not know the types at compile time.
        //     It therefore pays for a castclass and for BOXING the decimal.
        var untypedMethod = new DynamicMethod("GetSalaryUntyped", typeof(object), [typeof(object)]);
        ILGenerator il = untypedMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, typeof(Employee));
        il.Emit(OpCodes.Callvirt, getter);
        il.Emit(OpCodes.Box, typeof(decimal));
        il.Emit(OpCodes.Ret);
        var emittedUntyped = untypedMethod.CreateDelegate<Func<object, object>>();

        // (e) emitted IL with the SAME signature as the cached delegate, so the two are
        //     directly comparable: no cast, no box.
        var typedMethod = new DynamicMethod("GetSalaryTyped", typeof(decimal), [typeof(Employee)]);
        ILGenerator il2 = typedMethod.GetILGenerator();
        il2.Emit(OpCodes.Ldarg_0);
        il2.Emit(OpCodes.Callvirt, getter);
        il2.Emit(OpCodes.Ret);
        var emittedTyped = typedMethod.CreateDelegate<Func<Employee, decimal>>();

        // (c) a delegate obtained from the MethodInfo - no IL written by hand
        var fromMethodInfo = getter.CreateDelegate<Func<Employee, decimal>>();

        Console.WriteLine("Reading one property " + iterations.ToString("N0") + " times.");
        Console.WriteLine("Warming up (so the JIT has compiled everything before we measure)...");

        // Warm up every path.
        for (int i = 0; i < 20_000; i++)
        {
            _ = employee.Salary;
            _ = property.GetValue(employee);
            _ = getter.Invoke(employee, null);
            _ = fromMethodInfo(employee);
            _ = emittedTyped(employee);
            _ = emittedUntyped(employee);
        }

        long direct = Time(iterations, () => { _ = employee.Salary; });
        long reflectionProperty = Time(iterations, () => { _ = property.GetValue(employee); });
        long reflectionInvoke = Time(iterations, () => { _ = getter.Invoke(employee, null); });
        long delegateFromInfo = Time(iterations, () => { _ = fromMethodInfo(employee); });
        long emittedTypedTime = Time(iterations, () => { _ = emittedTyped(employee); });
        long emittedUntypedTime = Time(iterations, () => { _ = emittedUntyped(employee); });

        Console.WriteLine();
        Console.WriteLine("  technique                                total ms    relative to direct");
        Console.WriteLine("  -----------------------------------------------------------------------");
        Report("direct C# property access", direct, direct);
        Report("PropertyInfo.GetValue", reflectionProperty, direct);
        Report("MethodInfo.Invoke", reflectionInvoke, direct);
        Report("cached delegate (typed)", delegateFromInfo, direct);
        Report("emitted IL (typed, no box)", emittedTypedTime, direct);
        Report("emitted IL (object->object, boxes)", emittedUntypedTime, direct);

        Console.WriteLine();
        Console.WriteLine("How to read this:");
        Console.WriteLine("  - Reflection is slow PER CALL because every call re-does argument binding,");
        Console.WriteLine("    access checks and boxing of arguments and return value.");
        Console.WriteLine("  - Emitted IL and a cached delegate land in the same speed class, both far");
        Console.WriteLine("    closer to direct access than to reflection. The work happened ONCE.");
        Console.WriteLine("  - Compare the last two rows carefully. Same property, same technique; the only");
        Console.WriteLine("    difference is the signature. The object->object version pays for a castclass");
        Console.WriteLine("    and a decimal box on every call, and that alone costs more than the emit does.");
        Console.WriteLine("    Lesson: boxing and signature shape usually dominate - not 'reflection vs emit'.");
        Console.WriteLine("  - A cached delegate from MethodInfo gets emit-level speed with NO IL at all.");
        Console.WriteLine("    Try this FIRST; reach for Emit only when the shape is not fixed at compile time.");
        Console.WriteLine("  - Generation itself is not free: a DynamicMethod costs microseconds to build and");
        Console.WriteLine("    JIT. If you call the result once, reflection wins. Amortise, or do not bother.");
        Console.WriteLine();
        Console.WriteLine("These numbers are from THIS machine and this run, and Debug vs Release matters.");
        Console.WriteLine("Treat the ratios as the lesson, not the absolute milliseconds. For real");
        Console.WriteLine("measurements use BenchmarkDotNet, not Stopwatch in a loop.");
    }

    private static long Time(int iterations, Action action)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static void Report(string label, long elapsed, long baseline)
    {
        string relative = baseline == 0 ? "n/a" : (elapsed / Math.Max(1.0, baseline)).ToString("N1") + "x";
        Console.WriteLine($"  {label,-38}{elapsed,8}    {relative,8}");
    }

    // =====================================================================
    // 8. Broken IL: what the failure looks like and when it happens.
    // =====================================================================
    private static void InvalidIl()
    {
        Console.WriteLine("Case A: a method that never returns (no 'ret' at the end).");
        var missingRet = new DynamicMethod("MissingRet", typeof(int), [typeof(int), typeof(int)]);
        ILGenerator il1 = missingRet.GetILGenerator();
        il1.Emit(OpCodes.Ldarg_0);
        il1.Emit(OpCodes.Ldarg_1);
        il1.Emit(OpCodes.Add);
        // deliberately NO il1.Emit(OpCodes.Ret);

        try
        {
            var broken = missingRet.CreateDelegate<Func<int, int, int>>();
            Console.WriteLine("  CreateDelegate succeeded - nothing has been verified yet.");
            Console.WriteLine("  Calling it...");
            broken(1, 2);
            Console.WriteLine("  (unexpectedly worked)");
        }
        catch (InvalidProgramException ex)
        {
            Console.WriteLine("  InvalidProgramException on FIRST CALL: " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("Case B: a stack TYPE mismatch - 'add' on an int32 and a string reference.");
        Console.WriteLine("This one is worse than an exception. Watch what actually happens.");
        var typeMismatch = new DynamicMethod("TypeMismatch", typeof(int), [typeof(int)]);
        ILGenerator il2 = typeMismatch.GetILGenerator();
        il2.Emit(OpCodes.Ldarg_0);
        il2.Emit(OpCodes.Ldstr, "not a number");
        il2.Emit(OpCodes.Add);
        il2.Emit(OpCodes.Ret);

        try
        {
            var broken = typeMismatch.CreateDelegate<Func<int, int>>();
            int first = broken(1);
            int second = broken(1);
            Console.WriteLine("  No exception. broken(1) returned " + first + ", and again " + second + ".");
            Console.WriteLine("  That number is the string's managed reference added to 1 as an integer.");
            Console.WriteLine("  It is meaningless, it is not stable across runs, and nothing warned us.");
        }
        catch (InvalidProgramException)
        {
            Console.WriteLine("  InvalidProgramException - the verifier rejected this one.");
        }

        Console.WriteLine();
        Console.WriteLine("The important detail for debugging: Emit() accepted both of these without complaint.");
        Console.WriteLine("Case A was caught by the JIT on first call. Case B was NOT caught at all:");
        Console.WriteLine("IL emitted by a fully-trusted DynamicMethod is not type-verified, so a mistake");
        Console.WriteLine("can silently produce garbage instead of failing. Memory-unsafe IL in this position");
        Console.WriteLine("can corrupt process state rather than throw.");
        Console.WriteLine();
        Console.WriteLine("That asymmetry is the real reason to be careful with Emit: your safety net is");
        Console.WriteLine("thinner than the C# compiler's, and in some cases there is no net at all.");
        Console.WriteLine();
        Console.WriteLine("How to debug emitted IL in practice:");
        Console.WriteLine("  - Track the expected stack depth yourself as you emit; assert it returns to 0.");
        Console.WriteLine("  - Emit the equivalent C#, compile it, and read the IL with ILSpy or ildasm first.");
        Console.WriteLine("  - Generate a PERSISTED assembly (example 06) and run ILVerify or ildasm on it.");
        Console.WriteLine("  - Keep emitted methods tiny; put real logic in normal C# helpers and 'call' them.");
    }

    // =====================================================================
    // 9. Honest limits.
    // =====================================================================
    private static void Limits()
    {
        Console.WriteLine("DynamicMethod vs MethodBuilder (example 06):");
        Console.WriteLine();
        Console.WriteLine("  DynamicMethod");
        Console.WriteLine("    - a single standalone method, no declaring type");
        Console.WriteLine("    - cheapest option; collectible, so it can be garbage collected");
        Console.WriteLine("    - can be given 'restricted skip visibility' to reach private members");
        Console.WriteLine("    - cannot be saved to disk, cannot define fields, types or multiple methods");
        Console.WriteLine("    - the right tool for accessors, converters, mappers, serializer fast paths");
        Console.WriteLine();
        Console.WriteLine("  AssemblyBuilder / TypeBuilder / MethodBuilder");
        Console.WriteLine("    - builds whole TYPES: fields, properties, interfaces, several methods");
        Console.WriteLine("    - needed for proxies and mock objects that must BE a type");
        Console.WriteLine("    - more ceremony; CreateType() must be called before use");
        Console.WriteLine();
        Console.WriteLine("Limits that apply to both:");
        Console.WriteLine("  - Native AOT: not available. IsDynamicCodeSupported is false and Emit throws.");
        Console.WriteLine("  - Trimming: the trimmer cannot see what your IL will reference, so targets may");
        Console.WriteLine("    be removed. Annotate with DynamicallyAccessedMembers or avoid trimming.");
        Console.WriteLine("  - Debuggability: no source, so breakpoints and readable stack frames are limited.");
        Console.WriteLine("  - Maintainability: IL is write-mostly. Expect the next developer to fear it.");
        Console.WriteLine("  - Startup cost: generating and JIT-ing takes time on the first call.");
        Console.WriteLine();
        Console.WriteLine("Decision order worth memorising:");
        Console.WriteLine("  1. Plain C#, generics or a cached delegate.");
        Console.WriteLine("  2. Expression trees - same speed class, readable, and AOT-friendlier (example 08).");
        Console.WriteLine("  3. A Roslyn source generator, if the shape is known at build time.");
        Console.WriteLine("  4. Reflection.Emit - only when the shape is known ONLY at run time.");
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
/// Builds an arithmetic delegate from an operator supplied as DATA at run time.
/// This is the smallest honest example of "the code could not have been written in advance".
/// </summary>
internal static class ArithmeticFactory
{
    public static Func<int, int, int> Build(string op)
    {
        OpCode opcode = op switch
        {
            "+" => OpCodes.Add,
            "-" => OpCodes.Sub,
            "*" => OpCodes.Mul,
            "/" => OpCodes.Div,
            _ => throw new ArgumentException("Unsupported operator: " + op, nameof(op))
        };

        var method = new DynamicMethod("Op" + op, typeof(int), [typeof(int), typeof(int)]);
        ILGenerator il = method.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(opcode);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<int, int, int>>();
    }
}

internal sealed class Point(int x, int y)
{
    public int X { get; } = x;

    public int Y { get; } = y;

    public override string ToString() => "Point(" + X + ", " + Y + ")";
}

internal sealed class Employee
{
    public string Name { get; set; } = string.Empty;

    public decimal Salary { get; set; }
}
