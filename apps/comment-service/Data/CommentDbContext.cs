using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Data;

public class CommentDbContext(DbContextOptions<CommentDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Author).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => x.ArticleId);
        });
    }

    public async Task SeedAsync()
    {
        if (await Comments.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var rows = new List<Comment>();
        for (var articleId = 1; articleId <= 40; articleId++)
        {
            rows.Add(new Comment
            {
                ArticleId = articleId,
                Author = "Alice",
                Text = $"First cached comment for article {articleId}.",
                CreatedAtUtc = now.AddMinutes(-articleId * 3)
            });
            rows.Add(new Comment
            {
                ArticleId = articleId,
                Author = "Bob",
                Text = $"Second cached comment for article {articleId}.",
                CreatedAtUtc = now.AddMinutes(-articleId * 2)
            });
        }

        Comments.AddRange(rows);
        await SaveChangesAsync();
    }
}
