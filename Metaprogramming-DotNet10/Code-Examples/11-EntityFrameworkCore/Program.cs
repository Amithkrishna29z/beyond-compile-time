using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Metaprogramming.EfCoreDemo;

/// <summary>
/// Part 4 of the session: EF Core as the most elaborate metaprogramming most .NET
/// developers use daily. Reflection builds the model; expression trees become SQL.
/// Uses SQLite in-memory, so there is nothing to install and nothing to clean up.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        // An in-memory SQLite database lives only as long as its connection is open,
        // so we hold one open for the whole program.
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<BloggingContext> options = new DbContextOptionsBuilder<BloggingContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new BloggingContext(options);

        Banner("1. ENTITY DISCOVERY - how types get into the model");
        EntityDiscovery(context);

        Banner("2. THE EF MODEL IS NOT THE CLR MODEL");
        ModelVersusClr(context);

        Banner("3. THE THREE WAYS TO SUPPLY METADATA");
        ConventionsAnnotationsFluent(context);

        Banner("4. THE SCHEMA EF GENERATED FROM THAT METADATA");
        CreateSchema(context, connection);

        Banner("5. EXPRESSION TREES BECOMING SQL");
        ExpressionToSql(context);

        Banner("6. WHEN TRANSLATION FAILS");
        TranslationFailure(context);

        Banner("7. CHANGE TRACKING AND SHADOW PROPERTIES");
        ChangeTracking(context);

        Banner("8. THE WHOLE PIPELINE, SUMMARISED");
        Summary();
    }

    // =====================================================================
    // 1. DbSet<T> properties are the entry points.
    // =====================================================================
    private static void EntityDiscovery(BloggingContext context)
    {
        Console.WriteLine("The DbSet<T> properties declared on BloggingContext, read with reflection:");
        foreach (PropertyInfo p in typeof(BloggingContext)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType.IsGenericType
                                 && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)))
        {
            Console.WriteLine("  DbSet<" + p.PropertyType.GetGenericArguments()[0].Name + "> " + p.Name);
        }

        Console.WriteLine();
        Console.WriteLine("The entity types EF actually put in the model:");

        IModel model = context.Model;
        foreach (IEntityType entityType in model.GetEntityTypes().OrderBy(e => e.ClrType.Name))
        {
            Console.WriteLine("  " + entityType.ClrType.Name.PadRight(8)
                + " -> table \"" + entityType.GetTableName() + "\""
                + ", key = " + string.Join(", ", entityType.FindPrimaryKey()?.Properties.Select(p => p.Name) ?? []));
        }

        Console.WriteLine();
        Console.WriteLine("How a type enters the model:");
        Console.WriteLine("  - a DbSet<T> property on the context (the usual way);");
        Console.WriteLine("  - modelBuilder.Entity<T>() in OnModelCreating;");
        Console.WriteLine("  - being REACHABLE by navigation from a type already in the model.");
        Console.WriteLine();
        Console.WriteLine("That third rule catches people out: adding a navigation property can pull a");
        Console.WriteLine("whole new type into the model and change your schema, with no DbSet in sight.");
        Console.WriteLine();
        Console.WriteLine("Note the model was built ONCE, on first use, and is cached for the lifetime of");
        Console.WriteLine("the application - the same 'discover once' pattern as MVC in example 10.");
    }

    // =====================================================================
    // 2. The key conceptual point the syllabus asks for.
    // =====================================================================
    private static void ModelVersusClr(BloggingContext context)
    {
        IEntityType postType = context.Model.FindEntityType(typeof(Post))!;

        Console.WriteLine("CLR metadata for Post - what the C# compiler emitted:");
        foreach (PropertyInfo p in typeof(Post).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine("  " + FriendlyTypeName(p.PropertyType).PadRight(12) + p.Name);
        }

        Console.WriteLine();
        Console.WriteLine("EF Core model metadata for Post - what EF decided to map:");
        foreach (IProperty p in postType.GetProperties())
        {
            string shadow = p.IsShadowProperty() ? "  <- SHADOW: no CLR property exists" : string.Empty;
            Console.WriteLine("  " + FriendlyTypeName(p.ClrType).PadRight(12) + p.Name.PadRight(16)
                + "column \"" + p.GetColumnName() + "\"" + shadow);
        }

        Console.WriteLine();
        Console.WriteLine("Compare the two lists carefully - they are DIFFERENT:");
        Console.WriteLine();
        Console.WriteLine("  WordCount        is a CLR property but NOT in the EF model  ([NotMapped])");
        Console.WriteLine("  Blog, Author     are CLR properties but are NAVIGATIONS, not scalar properties");
        Console.WriteLine("  LastUpdatedUtc   is in the EF model but is NOT a CLR property (shadow property)");

        Console.WriteLine();
        Console.WriteLine("Navigations are tracked separately from properties:");
        foreach (INavigation navigation in postType.GetNavigations())
        {
            Console.WriteLine("  " + navigation.Name.PadRight(10) + " -> " + navigation.TargetEntityType.ClrType.Name
                + (navigation.IsCollection ? " (collection)" : " (reference)"));
        }

        Console.WriteLine();
        Console.WriteLine("So: the EF Core model is a SEPARATE model that EF BUILDS BY READING CLR metadata,");
        Console.WriteLine("then augments with its own conventions and your configuration. It is not a view");
        Console.WriteLine("over reflection, and it is not limited to what reflection can see. Reflection is");
        Console.WriteLine("the input; IModel is the output, and the output is richer.");
    }

    // =====================================================================
    // 3. Conventions, annotations and fluent API, side by side.
    // =====================================================================
    private static void ConventionsAnnotationsFluent(BloggingContext context)
    {
        Console.WriteLine("Blog - mostly CONVENTION. Nothing was configured; EF inferred it all:");
        Describe(context, typeof(Blog));

        Console.WriteLine();
        Console.WriteLine("Post - DATA ANNOTATIONS. [Table], [Column], [MaxLength], [NotMapped]:");
        Describe(context, typeof(Post));

        Console.WriteLine();
        Console.WriteLine("Author - FLUENT API. No attributes on the class at all:");
        Describe(context, typeof(Author));

        Console.WriteLine();
        Console.WriteLine("Precedence, lowest to highest: CONVENTION < DATA ANNOTATION < FLUENT API.");
        Console.WriteLine("The fluent API always wins, which is why it is the right place for");
        Console.WriteLine("environment-specific or hard-to-express configuration.");
        Console.WriteLine();
        Console.WriteLine("Note the parallel with Part 1: convention is IMPLICIT metadata inferred from the");
        Console.WriteLine("declaration; annotations and fluent calls are EXPLICIT metadata you supplied.");

        static void Describe(BloggingContext context, Type clrType)
        {
            IEntityType entityType = context.Model.FindEntityType(clrType)!;

            Console.WriteLine("  table: " + entityType.GetTableName());
            foreach (IProperty p in entityType.GetProperties())
            {
                int? maxLength = p.GetMaxLength();
                Console.WriteLine("    " + p.Name.PadRight(16)
                    + "column=" + (p.GetColumnName() + ",").PadRight(18)
                    + "type=" + (p.GetColumnType() + ",").PadRight(16)
                    + "nullable=" + p.IsNullable
                    + (maxLength is null ? "" : ", maxLength=" + maxLength));
            }
        }
    }

    // =====================================================================
    // 4. The metadata turned into DDL, then data.
    // =====================================================================
    private static void CreateSchema(BloggingContext context, SqliteConnection connection)
    {
        // EnsureCreated builds the schema from the model. In a real app you would use
        // migrations, which generate the same DDL but version it.
        context.Database.EnsureCreated();

        Console.WriteLine("The DDL EF Core generated from the model (read back from SQLite itself):");
        Console.WriteLine();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT sql FROM sqlite_master WHERE type IN ('table','index') AND sql IS NOT NULL ORDER BY type DESC, name";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            foreach (string line in reader.GetString(0).Split('\n'))
            {
                Console.WriteLine("  " + line.TrimEnd());
            }

            Console.WriteLine();
        }

        Console.WriteLine("Notice in that SQL:");
        Console.WriteLine("  - \"blog_posts\" and \"post_id\" came from [Table] and [Column] on Post;");
        Console.WriteLine("  - \"authors\", \"full_name\", \"email\" came from the fluent API;");
        Console.WriteLine("  - \"Blogs\" kept its conventional name because nothing configured it;");
        Console.WriteLine("  - \"last_updated_utc\" exists although no C# property does (shadow property);");
        Console.WriteLine("  - there is no WordCount column, because of [NotMapped];");
        Console.WriteLine("  - the unique index on email came from HasIndex(...).IsUnique().");

        Seed(context);
    }

    private static void Seed(BloggingContext context)
    {
        var asha = new Author { Name = "Asha Menon", EmailAddress = "asha@example.com" };
        var ravi = new Author { Name = "Ravi Kumar", EmailAddress = "ravi@example.com" };

        var dotnetBlog = new Blog
        {
            Title = ".NET Notes",
            Url = "https://example.com/dotnet",
            CreatedOn = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var dataBlog = new Blog
        {
            Title = "Data Platform Diary",
            Url = "https://example.com/data",
            CreatedOn = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        dotnetBlog.Posts.Add(new Post
        {
            Title = "Reflection in practice",
            Content = "A short tour of System dot Reflection and its costs.",
            PublishedOn = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            Rating = 5,
            Author = asha
        });

        dotnetBlog.Posts.Add(new Post
        {
            Title = "Emitting IL at runtime",
            Content = "DynamicMethod, ILGenerator and when not to use them.",
            PublishedOn = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc),
            Rating = 4,
            Author = asha
        });

        dotnetBlog.Posts.Add(new Post
        {
            Title = "A draft nobody proof-read",
            Content = "Rough notes.",
            PublishedOn = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            Rating = 2,
            Author = ravi
        });

        dataBlog.Posts.Add(new Post
        {
            Title = "Expression trees and SQL",
            Content = "How IQueryable turns a lambda into a query.",
            PublishedOn = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            Rating = 5,
            Author = ravi
        });

        context.Blogs.AddRange(dotnetBlog, dataBlog);
        context.SaveChanges();

        Console.WriteLine("Seeded " + context.Blogs.Count() + " blogs and " + context.Posts.Count() + " posts.");
    }

    // =====================================================================
    // 5. The heart of it: a C# lambda becoming SQL.
    // =====================================================================
    private static void ExpressionToSql(BloggingContext context)
    {
        Console.WriteLine("A. A simple filter.");
        Console.WriteLine();
        Console.WriteLine("  C#:");
        Console.WriteLine("    context.Posts.Where(p => p.Rating >= 4)");
        Console.WriteLine();

        IQueryable<Post> highRated = context.Posts.Where(p => p.Rating >= 4);

        Console.WriteLine("  The expression tree EF was handed:");
        Console.WriteLine("    " + Shorten(highRated.Expression.ToString()));
        Console.WriteLine();
        Console.WriteLine("  The SQL EF produced (ToQueryString does NOT execute the query):");
        PrintSql(highRated.ToQueryString());

        Console.WriteLine();
        Console.WriteLine("  Note the extra 'Rating >= 0' predicate: that is the global query filter");
        Console.WriteLine("  declared with HasQueryFilter in OnModelCreating. It is stored in the MODEL");
        Console.WriteLine("  as an expression tree and combined into every query over Post.");

        Console.WriteLine();
        Console.WriteLine("B. A projection, a join and an ordering - still one SQL statement.");
        Console.WriteLine();
        Console.WriteLine("  C#:");
        Console.WriteLine("    context.Posts");
        Console.WriteLine("      .Where(p => p.Rating >= 4 && p.Blog!.Title.Contains(\".NET\"))");
        Console.WriteLine("      .OrderByDescending(p => p.PublishedOn)");
        Console.WriteLine("      .Select(p => new { p.Title, Blog = p.Blog!.Title, Author = p.Author!.Name })");
        Console.WriteLine();

        var projected = context.Posts
            .Where(p => p.Rating >= 4 && p.Blog!.Title.Contains(".NET"))
            .OrderByDescending(p => p.PublishedOn)
            .Select(p => new { p.Title, Blog = p.Blog!.Title, Author = p.Author!.Name });

        Console.WriteLine("  SQL:");
        PrintSql(projected.ToQueryString());

        Console.WriteLine();
        Console.WriteLine("  Results (NOW the query executes):");
        foreach (var row in projected)
        {
            Console.WriteLine("    " + row.Title + "  [" + row.Blog + ", " + row.Author + "]");
        }

        Console.WriteLine();
        Console.WriteLine("  EF translated the navigation p.Blog!.Title into an INNER JOIN, the nullable");
        Console.WriteLine("  p.Author!.Name into a LEFT JOIN, and the anonymous type into a SELECT list -");
        Console.WriteLine("  so it never loaded a whole Post entity.");
        Console.WriteLine();
        Console.WriteLine("  Look at how string.Contains was rendered: instr(\"b0\".\"Title\", '.NET') > 0.");
        Console.WriteLine("  That is a SQLite function, not LIKE. The PROVIDER chooses the translation,");
        Console.WriteLine("  and the SQL Server provider emits something different for the same C#.");
        Console.WriteLine("  This is why you should read the SQL your provider actually produces rather");
        Console.WriteLine("  than assume - ToQueryString() exists for precisely that.");

        Console.WriteLine();
        Console.WriteLine("C. Aggregation happening in the DATABASE, not in memory.");
        Console.WriteLine();

        var summary = context.Blogs
            .Select(b => new { b.Title, PostCount = b.Posts.Count, Best = b.Posts.Max(p => (int?)p.Rating) });

        Console.WriteLine("  SQL:");
        PrintSql(summary.ToQueryString());
        Console.WriteLine();
        Console.WriteLine("  Results:");
        foreach (var row in summary)
        {
            Console.WriteLine("    " + row.Title.PadRight(22) + "posts=" + row.PostCount + ", best rating=" + row.Best);
        }

        Console.WriteLine();
        Console.WriteLine("D. A composed, reusable predicate - the real reason this matters.");
        Console.WriteLine();

        // The SAME Expression<Func<...>> as example 08, now sent to a database.
        Expression<Func<Post, bool>> isRecommended = p => p.Rating >= 4;

        IQueryable<Post> recommended = context.Posts.Where(isRecommended);

        Console.WriteLine("  Expression<Func<Post, bool>> isRecommended = p => p.Rating >= 4;");
        Console.WriteLine("  context.Posts.Where(isRecommended)  ->");
        PrintSql(recommended.ToQueryString());

        Console.WriteLine();
        Console.WriteLine("  The identical object could instead be .Compile()d and run over a List<Post>");
        Console.WriteLine("  in memory, exactly as in example 08. One definition of 'recommended', two");
        Console.WriteLine("  execution engines. That is only possible because it is DATA, not a delegate.");
        Console.WriteLine();
        Console.WriteLine("  Watch the type, though: Where(Func<Post,bool>) on IEnumerable and");
        Console.WriteLine("  Where(Expression<Func<Post,bool>>) on IQueryable look identical in source.");
        Console.WriteLine("  An accidental .AsEnumerable() or .ToList() before .Where() switches you from");
        Console.WriteLine("  the second to the first, and the filtering silently moves from the database");
        Console.WriteLine("  into your process - after downloading every row. Demonstrated next.");

        Console.WriteLine();
        Console.WriteLine("E. The classic performance bug, made visible.");
        Console.WriteLine();
        Console.WriteLine("  GOOD: context.Posts.Where(p => p.Rating >= 4).ToList()");
        Console.WriteLine("    -> filtering in SQL:");
        PrintSql(context.Posts.Where(p => p.Rating >= 4).ToQueryString());
        Console.WriteLine();
        Console.WriteLine("  BAD : context.Posts.ToList().Where(p => p.Rating >= 4)");
        Console.WriteLine("    -> the SQL EF runs is just:");
        PrintSql(context.Posts.ToQueryString());
        Console.WriteLine("    -> every row is transferred, then C# discards most of them.");
        Console.WriteLine();
        Console.WriteLine("  Both produce the same answer. On a large table only one of them is usable.");
    }

    // =====================================================================
    // 6. Translation failure, triggered on purpose.
    // =====================================================================
    private static void TranslationFailure(BloggingContext context)
    {
        Console.WriteLine("Example 08 said: a valid expression tree is not necessarily a TRANSLATABLE one.");
        Console.WriteLine("Here is that failure for real.");
        Console.WriteLine();
        Console.WriteLine("  context.Posts.Where(p => IsProofRead(p.Content)).ToList()");
        Console.WriteLine("  where IsProofRead is a local C# method with no SQL equivalent:");
        Console.WriteLine();

        try
        {
            List<Post> result = context.Posts.Where(p => IsProofRead(p.Content)).ToList();
            Console.WriteLine("  (unexpectedly succeeded with " + result.Count + " rows)");
        }
        catch (InvalidOperationException ex)
        {
            string message = ex.Message.Split(". ")[0];
            Console.WriteLine("  InvalidOperationException:");
            Console.WriteLine("    " + Shorten(message, 220));
        }

        Console.WriteLine();
        Console.WriteLine("EF Core refuses rather than silently downloading the table and filtering in");
        Console.WriteLine("memory. That refusal is deliberate: the silent version was the behaviour of");
        Console.WriteLine("older EF Core releases and it caused serious production surprises.");
        Console.WriteLine();
        Console.WriteLine("The three fixes, in order of preference:");
        Console.WriteLine("  1. Rewrite the predicate using constructs EF can translate.");
        Console.WriteLine("  2. Narrow in SQL first, then finish in memory, being explicit about it:");

        List<Post> hybrid = context.Posts
            .Where(p => p.Rating >= 3)          // translated, runs in SQL
            .AsEnumerable()                      // the boundary, stated out loud
            .Where(p => IsProofRead(p.Content))  // ordinary LINQ to Objects
            .ToList();

        Console.WriteLine("       context.Posts.Where(p => p.Rating >= 3).AsEnumerable()");
        Console.WriteLine("                    .Where(p => IsProofRead(p.Content)).ToList()");
        Console.WriteLine("       -> " + hybrid.Count + " post(s): " + string.Join(", ", hybrid.Select(p => p.Title)));
        Console.WriteLine("  3. Push the logic into the database as a function and map it with");
        Console.WriteLine("     modelBuilder.HasDbFunction.");
        Console.WriteLine();
        Console.WriteLine("Also worth knowing: what IS translatable changes between EF versions and");
        Console.WriteLine("between providers. A query that works on SQL Server may fail on SQLite.");
        Console.WriteLine("The provider, not EF Core alone, decides.");
    }

    /// <summary>A plain C# method. SQL has never heard of it.</summary>
    private static bool IsProofRead(string content) => content.EndsWith('.') && content.Length > 20;

    // =====================================================================
    // 7. Change tracking, and the shadow property in action.
    // =====================================================================
    private static void ChangeTracking(BloggingContext context)
    {
        Post post = context.Posts.First(p => p.Title == "Reflection in practice");

        Console.WriteLine("Loaded: \"" + post.Title + "\" (rating " + post.Rating + ")");
        Console.WriteLine("  tracked state: " + context.Entry(post).State);
        Console.WriteLine();

        post.Rating = 3;

        Console.WriteLine("After 'post.Rating = 3;' - we touched a plain C# property, nothing else:");
        Console.WriteLine("  tracked state: " + context.Entry(post).State);
        Console.WriteLine();
        Console.WriteLine("  Modified properties EF detected by comparing against its snapshot:");
        foreach (var property in context.Entry(post).Properties.Where(p => p.IsModified))
        {
            Console.WriteLine("    " + property.Metadata.Name
                + ": \"" + property.OriginalValue + "\" -> \"" + property.CurrentValue + "\"");
        }

        Console.WriteLine();
        Console.WriteLine("EF kept the ORIGINAL values so it can generate a minimal UPDATE. It obtained");
        Console.WriteLine("them by reading the entity's properties when it materialised the object.");

        Console.WriteLine();
        Console.WriteLine("Reading the shadow property - there is no post.LastUpdatedUtc to read,");
        Console.WriteLine("so it has to be reached through the change tracker by NAME:");
        Console.WriteLine("  before SaveChanges: " + context.Entry(post).Property<DateTime>("LastUpdatedUtc").CurrentValue);

        context.SaveChanges();

        Console.WriteLine("  after  SaveChanges: " + context.Entry(post).Property<DateTime>("LastUpdatedUtc").CurrentValue);
        Console.WriteLine("  (BloggingContext.SaveChanges sets it for every Added/Modified Post)");
        Console.WriteLine("  tracked state now : " + context.Entry(post).State);

        Console.WriteLine();
        Console.WriteLine("A quick note on how the values get in and out of your objects: EF Core does not");
        Console.WriteLine("call PropertyInfo.GetValue per property per row. It builds accessor delegates");
        Console.WriteLine("once per property - from expression trees on a JIT runtime - and reuses them.");
        Console.WriteLine("Same lesson as examples 05 and 10: reflect once, then run compiled code.");
    }

    // =====================================================================
    // 8. Tie it together.
    // =====================================================================
    private static void Summary()
    {
        Console.WriteLine("  stage                     mechanism                         when");
        Console.WriteLine("  --------------------------------------------------------------------------");
        Console.WriteLine("  find DbSet<T> properties  System.Reflection                 startup (once)");
        Console.WriteLine("  read annotations          GetCustomAttributes               startup (once)");
        Console.WriteLine("  apply conventions         EF's convention set               startup (once)");
        Console.WriteLine("  apply fluent config       your OnModelCreating              startup (once)");
        Console.WriteLine("  build IModel              EF model builder                  startup (once)");
        Console.WriteLine("  build accessors           expression trees -> delegates     first use, cached");
        Console.WriteLine("  translate a query         ExpressionVisitor chain -> SQL    per query shape,");
        Console.WriteLine("                                                              then cached");
        Console.WriteLine("  materialise rows          cached accessor delegates         per row");
        Console.WriteLine();
        Console.WriteLine("Every technique from this session appears in that table:");
        Console.WriteLine("  Part 2 reflection      -> discovering DbSets and reading attributes");
        Console.WriteLine("  Part 3 code generation -> accessor delegates built at run time");
        Console.WriteLine("  Part 4 expression trees-> LINQ becoming SQL");
        Console.WriteLine();
        Console.WriteLine("And the recurring rule, for the third time in Part 4: DISCOVER ONCE, CACHE,");
        Console.WriteLine("THEN EXECUTE COMPILED CODE. That is how a metadata-driven framework can be");
        Console.WriteLine("both completely general and fast enough to use in production.");
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static void PrintSql(string sql)
    {
        foreach (string line in sql.Split('\n'))
        {
            Console.WriteLine("    " + line.TrimEnd());
        }
    }

    /// <summary>Renders Nullable&lt;int&gt; as "int?" instead of "Nullable`1".</summary>
    private static string FriendlyTypeName(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        return underlying is not null ? underlying.Name + "?" : type.Name;
    }

    private static string Shorten(string text, int max = 160)
    {
        string collapsed = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return collapsed.Length <= max ? collapsed : collapsed[..max] + " ...";
    }

    private static void Banner(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }
}
