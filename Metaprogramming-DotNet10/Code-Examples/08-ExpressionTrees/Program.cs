using System.Linq.Expressions;
using System.Reflection;

namespace Metaprogramming.ExpressionTreesDemo;

/// <summary>
/// Part 4 of the session: expression trees - code represented as data.
/// Shows the delegate/expression distinction, walks the node graph, compiles and runs
/// a tree, builds one by hand, rewrites one with a visitor, and ends with IQueryable.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. THE SAME LAMBDA, TWO COMPLETELY DIFFERENT THINGS");
        DelegateVersusExpression();

        Banner("2. WALKING THE TREE");
        WalkTheTree();

        Banner("3. Compile() - FROM DATA BACK TO EXECUTABLE CODE");
        CompileAndRun();

        Banner("4. BUILDING AN EXPRESSION FROM SCRATCH (no lambda syntax at all)");
        BuildByHand();

        Banner("5. A DYNAMIC FILTER BUILT FROM RUNTIME INPUT");
        DynamicFilter();

        Banner("6. REWRITING A TREE WITH ExpressionVisitor");
        RewriteWithVisitor();

        Banner("7. IQueryable - WHY LINQ PROVIDERS NEED EXPRESSIONS");
        QueryableDemo();

        Banner("8. WHAT EXPRESSION TREES CANNOT DO");
        Limits();
    }

    // =====================================================================
    // 1. Func<T> vs Expression<Func<T>>.
    // =====================================================================
    private static void DelegateVersusExpression()
    {
        // A delegate: the compiler turns the lambda into a METHOD and hands you a pointer.
        // The body is IL. You can call it. You cannot read it.
        Func<int, bool> asDelegate = n => n > 18;

        // An expression tree: the compiler turns the SAME lambda into code that BUILDS AN
        // OBJECT GRAPH describing the lambda. You can read it, change it, translate it.
        Expression<Func<int, bool>> asExpression = n => n > 18;

        Console.WriteLine("Source text is identical in both cases: n => n > 18");
        Console.WriteLine();
        Console.WriteLine("Func<int, bool>:");
        Console.WriteLine("  runtime type : " + asDelegate.GetType().Name);
        Console.WriteLine("  can call it  : asDelegate(21) = " + asDelegate(21));
        Console.WriteLine("  can read it  : no - the body is compiled IL");
        Console.WriteLine("  Method.Name  : " + asDelegate.Method.Name + "  (a compiler-generated method)");

        Console.WriteLine();
        Console.WriteLine("Expression<Func<int, bool>>:");
        Console.WriteLine("  runtime type : " + asExpression.GetType().Name);
        Console.WriteLine("  NodeType     : " + asExpression.NodeType);
        Console.WriteLine("  Body         : " + asExpression.Body);
        Console.WriteLine("  Body.NodeType: " + asExpression.Body.NodeType);
        Console.WriteLine("  Parameters   : " + string.Join(", ", asExpression.Parameters.Select(p => p.Type.Name + " " + p.Name)));
        Console.WriteLine("  ToString()   : " + asExpression);
        Console.WriteLine("  can call it  : not directly - Compile() first");

        Console.WriteLine();
        Console.WriteLine("The difference is decided entirely by the TYPE you assign the lambda to.");
        Console.WriteLine("Func<...>       -> compile the body to IL");
        Console.WriteLine("Expression<...> -> compile code that builds a description of the body");
        Console.WriteLine();
        Console.WriteLine("This is the one place where C# itself is doing compile-time metaprogramming");
        Console.WriteLine("on your behalf: it hands you your own source code as inspectable data.");
    }

    // =====================================================================
    // 2. The tree really is a tree.
    // =====================================================================
    private static void WalkTheTree()
    {
        Expression<Func<Customer, bool>> predicate =
            c => c.Age > 18 && c.Country == "IN";

        Console.WriteLine("Expression: c => c.Age > 18 && c.Country == \"IN\"");
        Console.WriteLine();
        Console.WriteLine("Structure:");
        PrintNode(predicate.Body, indent: 1);

        Console.WriteLine();
        Console.WriteLine("Reading one branch by hand, to show the nodes are strongly typed:");

        var and = (BinaryExpression)predicate.Body;
        var left = (BinaryExpression)and.Left;                   // c.Age > 18
        var member = (MemberExpression)left.Left;                // c.Age
        var constant = (ConstantExpression)left.Right;           // 18

        Console.WriteLine("  and.NodeType            = " + and.NodeType);
        Console.WriteLine("  left.NodeType           = " + left.NodeType);
        Console.WriteLine("  member.Member.Name      = " + member.Member.Name
            + "   (a " + member.Member.MemberType + " on " + member.Member.DeclaringType?.Name + ")");
        Console.WriteLine("  constant.Value          = " + constant.Value + " (" + constant.Type.Name + ")");

        Console.WriteLine();
        Console.WriteLine("Because every node is an object, a library can ask questions such as");
        Console.WriteLine("'which property is being compared, and to what?' - and that is precisely");
        Console.WriteLine("the question a SQL translator has to answer.");
    }

    private static void PrintNode(Expression node, int indent)
    {
        string pad = new(' ', indent * 2);

        switch (node)
        {
            case BinaryExpression binary:
                Console.WriteLine(pad + binary.NodeType + "   (BinaryExpression, " + binary.Type.Name + ")");
                PrintNode(binary.Left, indent + 1);
                PrintNode(binary.Right, indent + 1);
                break;

            case MemberExpression memberAccess:
                Console.WriteLine(pad + "MemberAccess  -> " + memberAccess.Member.Name
                    + " : " + memberAccess.Type.Name);
                PrintNode(memberAccess.Expression!, indent + 1);
                break;

            case ConstantExpression constant:
                Console.WriteLine(pad + "Constant      -> " + (constant.Value ?? "null")
                    + " : " + constant.Type.Name);
                break;

            case ParameterExpression parameter:
                Console.WriteLine(pad + "Parameter     -> " + parameter.Name + " : " + parameter.Type.Name);
                break;

            case MethodCallExpression call:
                Console.WriteLine(pad + "Call          -> " + call.Method.Name);
                foreach (Expression argument in call.Arguments)
                {
                    PrintNode(argument, indent + 1);
                }

                break;

            default:
                Console.WriteLine(pad + node.NodeType + "   (" + node.GetType().Name + ")");
                break;
        }
    }

    // =====================================================================
    // 3. Compile(): the round trip back to machine code.
    // =====================================================================
    private static void CompileAndRun()
    {
        Expression<Func<Customer, bool>> predicate = c => c.Age > 18 && c.Country == "IN";

        // Compile walks the tree and generates IL for it, then JITs it.
        // On CoreCLR this uses the same lightweight code generation as DynamicMethod.
        Func<Customer, bool> compiled = predicate.Compile();

        Customer[] customers =
        [
            new("Asha", 29, "IN"),
            new("Ravi", 16, "IN"),
            new("Mia", 34, "US")
        ];

        Console.WriteLine("predicate.Compile() returned a " + compiled.GetType().Name);
        Console.WriteLine();
        foreach (Customer c in customers)
        {
            Console.WriteLine($"  {c.Name,-6} age {c.Age,2} {c.Country}  -> {compiled(c)}");
        }

        Console.WriteLine();
        Console.WriteLine("Important cost note: Compile() is EXPENSIVE - it generates and JITs code.");
        Console.WriteLine("Compile once, cache the delegate, call it many times. Compiling inside a loop");
        Console.WriteLine("or per web request is one of the most common performance mistakes with this API.");
        Console.WriteLine();
        Console.WriteLine("Note also: Compile() needs runtime code generation, so under Native AOT it is");
        Console.WriteLine("not available in the usual sense. Expression trees can still be BUILT and READ");
        Console.WriteLine("there - which is why EF Core can translate queries to SQL under AOT, because");
        Console.WriteLine("translation only reads the tree. It is Compile() that needs the JIT.");
    }

    // =====================================================================
    // 4. Building the tree with the factory methods.
    // =====================================================================
    private static void BuildByHand()
    {
        Console.WriteLine("Goal: build (a, b) => (a + b) * 2 without writing a lambda.");
        Console.WriteLine();

        // Parameters are nodes you create and then reuse by reference.
        ParameterExpression a = Expression.Parameter(typeof(int), "a");
        ParameterExpression b = Expression.Parameter(typeof(int), "b");
        Console.WriteLine("  Expression.Parameter(typeof(int), \"a\")   -> " + a);
        Console.WriteLine("  Expression.Parameter(typeof(int), \"b\")   -> " + b);

        BinaryExpression sum = Expression.Add(a, b);
        Console.WriteLine("  Expression.Add(a, b)                     -> " + sum);

        ConstantExpression two = Expression.Constant(2, typeof(int));
        Console.WriteLine("  Expression.Constant(2)                   -> " + two);

        BinaryExpression doubled = Expression.Multiply(sum, two);
        Console.WriteLine("  Expression.Multiply(sum, two)            -> " + doubled);

        Expression<Func<int, int, int>> lambda = Expression.Lambda<Func<int, int, int>>(doubled, a, b);
        Console.WriteLine("  Expression.Lambda<Func<int,int,int>>(..)  -> " + lambda);

        Func<int, int, int> fn = lambda.Compile();

        Console.WriteLine();
        Console.WriteLine("  fn(3, 4)   = " + fn(3, 4));
        Console.WriteLine("  fn(10, 11) = " + fn(10, 11));

        Console.WriteLine();
        Console.WriteLine("Compare this with example 05, which built the same kind of function in IL:");
        Console.WriteLine("  Reflection.Emit : ldarg.0 / ldarg.1 / add / ldc.i4.2 / mul / ret");
        Console.WriteLine("  Expression tree : Add(a, b) -> Multiply(..., 2) -> Lambda");
        Console.WriteLine();
        Console.WriteLine("Same speed class after compilation, but the expression version is type-checked");
        Console.WriteLine("as you build it, readable, and it can be printed. Expression.Add would THROW");
        Console.WriteLine("immediately if you passed it an int and a string - IL would have silently");
        Console.WriteLine("produced garbage, as example 05 demonstrated.");
        Console.WriteLine();
        Console.WriteLine("Prefer expression trees over Reflection.Emit whenever you need a FUNCTION.");
        Console.WriteLine("Reach for Emit only when you need a TYPE, which Expression cannot create.");
    }

    // =====================================================================
    // 5. The practical use: a filter whose shape comes from user input.
    // =====================================================================
    private static void DynamicFilter()
    {
        Customer[] customers =
        [
            new("Asha", 29, "IN"),
            new("Ravi", 16, "IN"),
            new("Mia", 34, "US"),
            new("Deepa", 41, "IN"),
            new("Tom", 22, "UK")
        ];

        // Pretend this came from a query string: ?minAge=21&country=IN
        var filters = new Dictionary<string, string>
        {
            ["Age"] = ">21",
            ["Country"] = "=IN"
        };

        Console.WriteLine("Filters supplied at runtime: " + string.Join(", ", filters.Select(f => f.Key + f.Value)));
        Console.WriteLine();

        Expression<Func<Customer, bool>> predicate = PredicateBuilder.Build<Customer>(filters);

        Console.WriteLine("Expression built from those filters:");
        Console.WriteLine("  " + predicate);
        Console.WriteLine();

        Func<Customer, bool> compiled = predicate.Compile();

        Console.WriteLine("Matches:");
        foreach (Customer c in customers.Where(compiled))
        {
            Console.WriteLine("  " + c.Name + " (" + c.Age + ", " + c.Country + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Why an expression and not a Func here? Because the SAME object can either be");
        Console.WriteLine("compiled and run in memory, as above, or handed to EF Core and turned into a");
        Console.WriteLine("SQL WHERE clause. A Func could only ever do the first.");
    }

    // =====================================================================
    // 6. ExpressionVisitor: read, and write back a modified tree.
    // =====================================================================
    private static void RewriteWithVisitor()
    {
        Expression<Func<Customer, bool>> original = c => c.Age > 18 && c.Country == "IN";

        Console.WriteLine("Original : " + original);

        // Trees are IMMUTABLE. A visitor does not edit; it produces a new tree.
        var rewriter = new ConstantRaiser(delta: 3);
        var rewritten = (Expression<Func<Customer, bool>>)rewriter.Visit(original);

        Console.WriteLine("Rewritten: " + rewritten + "   (every int constant raised by 3)");

        Func<Customer, bool> before = original.Compile();
        Func<Customer, bool> after = rewritten.Compile();

        var twentyYearOld = new Customer("Nithya", 20, "IN");

        Console.WriteLine();
        Console.WriteLine("Nithya is 20 and in IN:");
        Console.WriteLine("  original (Age > 18) -> " + before(twentyYearOld));
        Console.WriteLine("  rewritten (Age > 21)-> " + after(twentyYearOld));

        Console.WriteLine();
        Console.WriteLine("Also useful: a visitor that only COUNTS things, without rewriting.");
        var counter = new NodeCounter();
        counter.Visit(original);
        Console.WriteLine("  node census of the original tree:");
        foreach ((ExpressionType type, int count) in counter.Counts.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine("    " + type + " x " + count);
        }

        Console.WriteLine();
        Console.WriteLine("ExpressionVisitor is the standard way to process trees. EF Core's query");
        Console.WriteLine("pipeline is a long chain of visitors, each one rewriting the tree a little");
        Console.WriteLine("closer to something that can be printed as SQL.");
    }

    // =====================================================================
    // 7. IQueryable: the same query, two radically different fates.
    // =====================================================================
    private static void QueryableDemo()
    {
        Customer[] customers =
        [
            new("Asha", 29, "IN"),
            new("Ravi", 16, "IN"),
            new("Mia", 34, "US"),
            new("Deepa", 41, "IN")
        ];

        // IEnumerable<T>.Where takes a Func<T, bool>. LINQ to Objects just runs it.
        IEnumerable<Customer> asEnumerable = customers
            .Where(c => c.Age > 18 && c.Country == "IN");

        Console.WriteLine("IEnumerable<Customer>.Where(...)");
        Console.WriteLine("  Where's parameter type: Func<Customer, bool>");
        Console.WriteLine("  the lambda is COMPILED CODE, executed per element, in this process");
        Console.WriteLine("  result: " + string.Join(", ", asEnumerable.Select(c => c.Name)));

        Console.WriteLine();

        // IQueryable<T>.Where takes an Expression<Func<T, bool>>. The provider gets the TREE.
        IQueryable<Customer> asQueryable = customers.AsQueryable()
            .Where(c => c.Age > 18 && c.Country == "IN")
            .OrderBy(c => c.Name);

        Console.WriteLine("IQueryable<Customer>.Where(...).OrderBy(...)");
        Console.WriteLine("  Where's parameter type: Expression<Func<Customer, bool>>");
        Console.WriteLine("  nothing has executed yet. What exists is an expression tree:");
        Console.WriteLine();
        Console.WriteLine("  " + asQueryable.Expression.ToString().Replace(".Where", Environment.NewLine + "    .Where")
            .Replace(".OrderBy", Environment.NewLine + "    .OrderBy"));

        Console.WriteLine();
        Console.WriteLine("  provider: " + asQueryable.Provider.GetType().Name);
        Console.WriteLine("  element : " + asQueryable.ElementType.Name);

        // Enumerating is what finally executes it. Here the provider is LINQ to Objects,
        // so it compiles the tree and runs it in memory.
        Console.WriteLine("  result (on enumeration): " + string.Join(", ", asQueryable.Select(c => c.Name)));

        Console.WriteLine();
        Console.WriteLine("Swap the provider and the SAME tree has a different destiny:");
        Console.WriteLine("  EnumerableQuery (here) -> Compile() the tree, run it in memory");
        Console.WriteLine("  EF Core                -> translate the tree to SQL, run it in the database");
        Console.WriteLine("  an HTTP/OData provider -> translate the tree to a URL query string");
        Console.WriteLine();
        Console.WriteLine("That substitutability is the entire reason LINQ providers can exist, and it");
        Console.WriteLine("works only because the query arrived as DATA rather than as compiled code.");
        Console.WriteLine("Example 11 shows the EF Core side, with the real generated SQL.");
    }

    // =====================================================================
    // 8. The limits, stated plainly.
    // =====================================================================
    private static void Limits()
    {
        Console.WriteLine("A. Not every C# construct can appear in an Expression<TDelegate>.");
        Console.WriteLine("   The C# compiler REJECTS these at build time inside an expression lambda:");
        Console.WriteLine("     - statement bodies:   x => { var y = x * 2; return y; }");
        Console.WriteLine("     - assignment:         x => x = 5");
        Console.WriteLine("     - await:              x => await FooAsync(x)");
        Console.WriteLine("     - ref/out arguments, unsafe code, lambdas with dynamic");
        Console.WriteLine("     - null-propagating ?. and some pattern-matching forms");
        Console.WriteLine("   Those are compile errors, not runtime failures. (The underlying");
        Console.WriteLine("   System.Linq.Expressions API can represent blocks and assignment via");
        Console.WriteLine("   Expression.Block and Expression.Assign - the C# LAMBDA SYNTAX cannot.)");
        Console.WriteLine();
        Console.WriteLine("B. Not every valid expression tree can be translated by a given provider.");
        Console.WriteLine("   This is a separate, later failure, and it surfaces at RUNTIME:");

        Customer[] customers = [new("Asha", 29, "IN")];

        try
        {
            // A local C# method has no SQL equivalent. LINQ to Objects copes by compiling
            // and running it; a database provider would refuse to translate it.
            var result = customers.AsQueryable()
                .Where(c => IsVipCustomer(c.Name))
                .ToList();

            Console.WriteLine("     customers.AsQueryable().Where(c => IsVipCustomer(c.Name)) ->");
            Console.WriteLine("       LINQ to Objects ran it happily: " + result.Count + " match(es).");
            Console.WriteLine("       EF Core would throw InvalidOperationException: 'could not be translated'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("     " + ex.GetType().Name + ": " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("   The distinction worth teaching:");
        Console.WriteLine("     'cannot be EXPRESSED as a tree' -> a C# compile error.");
        Console.WriteLine("     'cannot be TRANSLATED to SQL'   -> a provider exception at run time.");
        Console.WriteLine("   Those are different failures with different fixes, and they are easy to");
        Console.WriteLine("   confuse because both are described as 'expression trees do not support that'.");
        Console.WriteLine();
        Console.WriteLine("C. Compile() is slow and needs dynamic code support (no Native AOT).");
        Console.WriteLine("D. A tree is immutable: every 'change' allocates a new tree.");
        Console.WriteLine("E. Debugging a compiled expression gives you no source to step through.");
    }

    private static bool IsVipCustomer(string name) => name.StartsWith('A');

    private static void Banner(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }
}

internal sealed record Customer(string Name, int Age, string Country);

// =========================================================================
// Builds a predicate from a dictionary of runtime filters.
// =========================================================================

internal static class PredicateBuilder
{
    /// <summary>
    /// Turns {"Age": "&gt;21", "Country": "=IN"} into x =&gt; x.Age &gt; 21 &amp;&amp; x.Country == "IN".
    /// </summary>
    public static Expression<Func<T, bool>> Build<T>(IReadOnlyDictionary<string, string> filters)
    {
        // The single parameter shared by every sub-expression: x
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

        // Built up one clause at a time. Left null until the first clause so that the
        // printed expression does not carry a pointless leading "True AndAlso".
        Expression? body = null;

        foreach ((string propertyName, string filter) in filters)
        {
            PropertyInfo? property = typeof(T).GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null)
            {
                throw new ArgumentException(typeof(T).Name + " has no property '" + propertyName + "'.", nameof(filters));
            }

            // x.Age
            MemberExpression member = Expression.Property(parameter, property);

            string op = filter[..1];
            string rawValue = filter[1..];

            // Convert the text to the property's real CLR type, then make a constant node.
            // Typing the constant correctly is what keeps the comparison valid.
            object typedValue = Convert.ChangeType(rawValue, property.PropertyType);
            ConstantExpression constant = Expression.Constant(typedValue, property.PropertyType);

            Expression comparison = op switch
            {
                ">" => Expression.GreaterThan(member, constant),
                "<" => Expression.LessThan(member, constant),
                "=" => Expression.Equal(member, constant),
                _ => throw new ArgumentException("Unsupported operator '" + op + "'.", nameof(filters))
            };

            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        // No filters supplied: match everything.
        body ??= Expression.Constant(true);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

// =========================================================================
// Visitors.
// =========================================================================

/// <summary>Produces a new tree in which every int constant is increased by a delta.</summary>
internal sealed class ConstantRaiser(int delta) : ExpressionVisitor
{
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Type == typeof(int) && node.Value is int value)
        {
            // Return a DIFFERENT node. The original tree is untouched.
            return Expression.Constant(value + delta, typeof(int));
        }

        return base.VisitConstant(node);
    }
}

/// <summary>Counts how many nodes of each kind a tree contains.</summary>
internal sealed class NodeCounter : ExpressionVisitor
{
    public Dictionary<ExpressionType, int> Counts { get; } = [];

    public override Expression? Visit(Expression? node)
    {
        if (node is not null)
        {
            Counts[node.NodeType] = Counts.GetValueOrDefault(node.NodeType) + 1;
        }

        return base.Visit(node);
    }
}
