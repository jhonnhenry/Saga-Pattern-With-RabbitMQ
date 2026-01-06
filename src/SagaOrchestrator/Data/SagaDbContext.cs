using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace SagaOrchestrator.Data;

public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }

    public DbSet<SagaState> SagaStates { get; set; }
    public DbSet<SagaEvent> SagaEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SagaState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasMany(e => e.SagaEvents).WithOne(e => e.SagaState).HasForeignKey(e => e.SagaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SagaEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}

public static class SagaDbContextExtensions
{
    public static SagaState Add_Many_One(this SagaState sagaState)
    {
        return sagaState;
    }
}
