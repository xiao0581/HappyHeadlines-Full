using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDbContext(DbContextOptions<ArticleDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => x.PublishedAtUtc);
        });
    }

    public async Task SeedAsync()
    {
        if (await Articles.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(1, 40)
            .Select(i => new Article
            {
                Id = i,
                Title = $"Happy Headline #{i}",
                Content = $"This is demo article #{i}. It exists so you can demonstrate geographic caching, cache hits, cache misses and monitoring.",
                PublishedAtUtc = now.AddDays(-(i - 1))
            });

        Articles.AddRange(rows);
        await SaveChangesAsync();
    }
}
