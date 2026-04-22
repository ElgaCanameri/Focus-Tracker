using Microsoft.EntityFrameworkCore;
using Session.Application.Common;
using Session.Domain;
using Session.Domain.Entities;

namespace Session.Infrastructure
{
    public class AppDbContext : DbContext, IUnitOfWork
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Domain.Entities.Session> Sessions => Set<Domain.Entities.Session>();
        public DbSet<SessionShare> SessionShares => Set<SessionShare>();
        public DbSet<User> Users => Set<User>();
        public DbSet<MonthlyFocusEntity> MonthlyFocusItems => Set<MonthlyFocusEntity>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Domain.Entities.Session>(builder =>
            {
                builder.HasKey(s => s.Id);

                builder.Property(s => s.Topic)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(s => s.Mode)
                    .HasConversion<string>();

                builder.Property(s => s.UserId)
                    .IsRequired();

                builder.Property(s => s.IsDailyGoalAchieved)
                    .HasDefaultValue(false);

                builder.Property(s => s.PublicLinkToken)
                    .HasMaxLength(100);

                // value object mapping
                builder.OwnsOne(s => s.DurationMin, d =>
                {
                    d.Property(x => x.Value)
                     .HasColumnName("DurationMin")
                     .HasColumnType("decimal(5,2)");
                });

                // ignore domain events - not persisted
                builder.Ignore(s => s.DomainEvents);
            });

            modelBuilder.Entity<User>(builder =>
            {
                builder.HasKey(u => u.Id);

                builder.Property(u => u.ExternalId)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(u => u.Status)
                    .HasConversion<string>();

                builder.HasIndex(u => u.ExternalId)
                    .IsUnique();
            });

            modelBuilder.Entity<MonthlyFocusEntity>(builder =>
            {
                builder.HasKey(m => m.Id);
                builder.ToTable("MonthlyFocusProjection");

                builder.Property(m => m.UserId).IsRequired();
                builder.Property(m => m.Year).IsRequired();
                builder.Property(m => m.Month).IsRequired();
                builder.Property(m => m.TotalDurationMin)
                    .HasColumnType("decimal(10,2)");
            });

            modelBuilder.Entity<SessionShare>(builder =>
            {
                builder.HasKey(s => s.Id);
                builder.Property(s => s.SessionId).IsRequired();
                builder.Property(s => s.RecipientUserId).IsRequired();
                builder.Property(s => s.SharedAt).IsRequired();
            });

            modelBuilder.Entity<AuditLog>(builder =>
            {
                builder.HasKey(a => a.Id);
                builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
                builder.Property(a => a.TargetId).IsRequired().HasMaxLength(200);
                builder.Property(a => a.TargetType).IsRequired().HasMaxLength(100);
                builder.Property(a => a.PerformedBy).IsRequired().HasMaxLength(200);
                builder.Property(a => a.Details).HasMaxLength(500);
                builder.Property(a => a.OccurredOn).IsRequired();
            });
        }
    }
}
