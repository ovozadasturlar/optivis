using Microsoft.EntityFrameworkCore;
using OptiVis.Domain.Entities;

namespace OptiVis.Infrastructure.Persistence;

public class CdrDbContext : DbContext
{
    public CdrDbContext(DbContextOptions<CdrDbContext> options) : base(options)
    {
    }

    public DbSet<CallRecord> CallRecords => Set<CallRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CallRecord>(entity =>
        {
            entity.ToTable("cdr");
            entity.HasKey(e => e.Sequence);

            entity.Ignore(e => e.Disposition);
            entity.Ignore(e => e.IsIncoming);
            entity.Ignore(e => e.OperatorExtension);
            entity.Ignore(e => e.CallerNumber);
        });
    }
}
