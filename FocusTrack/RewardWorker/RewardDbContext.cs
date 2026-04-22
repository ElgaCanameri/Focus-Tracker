using Microsoft.EntityFrameworkCore;

namespace RewardWorker
{
    public class RewardDbContext : DbContext
    {
        public RewardDbContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Models.SessionRecord> Sessions => Set<Models.SessionRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Models.SessionRecord>(builder =>
            {
                builder.ToTable("Sessions");
                builder.HasKey(s => s.Id);
                builder.OwnsOne(s => s.DurationMin, d =>
                {
                    d.Property(x => x.Value)
                     .HasColumnName("DurationMin")
                     .HasColumnType("decimal(5,2)");
                });
            });
        }
    }
}
