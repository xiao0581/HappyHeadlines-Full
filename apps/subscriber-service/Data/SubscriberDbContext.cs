
using Microsoft.EntityFrameworkCore;
using SubscriberService.Models;

namespace SubscriberService.Data;

public class SubscriberDbContext
    : DbContext
{
    public SubscriberDbContext(
        DbContextOptions<SubscriberDbContext> options)
        : base(options)
    {
    }

    public DbSet<Subscriber> Subscribers =>
        Set<Subscriber>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email)
                .HasMaxLength(320)
                .IsRequired();

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.SubscribedAtUtc)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();
        });
    }
}
