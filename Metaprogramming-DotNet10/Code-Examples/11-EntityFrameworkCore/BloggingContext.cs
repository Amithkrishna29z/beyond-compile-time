using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Metaprogramming.EfCoreDemo;

// =========================================================================
// Entities. Three different ways of supplying metadata are used deliberately:
//   Blog   - mostly CONVENTION (EF infers nearly everything)
//   Post   - DATA ANNOTATIONS (attributes)
//   Author - FLUENT API (configured in OnModelCreating)
// =========================================================================

/// <summary>Configured almost entirely by convention.</summary>
public class Blog
{
    // Convention: a property named Id (or BlogId) becomes the primary key.
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Url { get; set; }

    public DateTime CreatedOn { get; set; }

    // Convention: a collection navigation establishes a one-to-many relationship,
    // and Post.BlogId is inferred as the foreign key.
    public List<Post> Posts { get; } = [];
}

/// <summary>Configured with data annotations - explicit metadata on the type itself.</summary>
[Table("blog_posts")]
public class Post
{
    [Key]
    [Column("post_id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")]
    public string Content { get; set; } = string.Empty;

    public DateTime PublishedOn { get; set; }

    public int Rating { get; set; }

    // Not mapped to a column at all - a computed, in-memory-only member.
    [NotMapped]
    public int WordCount => Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    public int BlogId { get; set; }

    public Blog? Blog { get; set; }

    public Author? Author { get; set; }

    public int? AuthorId { get; set; }
}

/// <summary>Configured with the fluent API only - no attributes on this class.</summary>
public class Author
{
    public int AuthorKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public string EmailAddress { get; set; } = string.Empty;

    public List<Post> Posts { get; } = [];
}

// =========================================================================
// The DbContext.
// =========================================================================

public class BloggingContext(DbContextOptions<BloggingContext> options) : DbContext(options)
{
    // EF Core reflects over these properties at model-building time to discover
    // the root entity types. Declaring a DbSet is how a type enters the model.
    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Author> Authors => Set<Author>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Fluent configuration for Author ---
        // Everything here could have been attributes; the fluent API exists because
        // some options have no attribute, and because it keeps persistence concerns
        // out of a domain class that should not reference EF at all.
        modelBuilder.Entity<Author>(author =>
        {
            author.ToTable("authors");

            // The property is called AuthorKey, so convention would NOT find a key.
            author.HasKey(a => a.AuthorKey);

            author.Property(a => a.Name)
                .HasColumnName("full_name")
                .HasMaxLength(120)
                .IsRequired();

            author.Property(a => a.EmailAddress)
                .HasColumnName("email")
                .HasMaxLength(200)
                .IsRequired();

            author.HasIndex(a => a.EmailAddress).IsUnique();
        });

        // --- Relationships ---
        modelBuilder.Entity<Post>()
            .HasOne(p => p.Blog)
            .WithMany(b => b.Posts)
            .HasForeignKey(p => p.BlogId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Author)
            .WithMany(a => a.Posts)
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- A SHADOW PROPERTY ---
        // A property that exists in EF's model but NOT on the CLR class. This is the
        // cleanest proof that the EF model is a separate thing from CLR metadata.
        modelBuilder.Entity<Post>()
            .Property<DateTime>("LastUpdatedUtc")
            .HasColumnName("last_updated_utc");

        // --- A query filter, which is an expression tree stored in the model ---
        modelBuilder.Entity<Post>()
            .HasQueryFilter(p => p.Rating >= 0);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        // Write the shadow property on the way out. Note we set it through the
        // CHANGE TRACKER by name, because there is no C# property to assign.
        foreach (var entry in ChangeTracker.Entries<Post>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property<DateTime>("LastUpdatedUtc").CurrentValue = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
            }
        }

        return base.SaveChanges();
    }
}
