using Microsoft.EntityFrameworkCore;
using Notification.Application.Common;

namespace Notification.Infrastructure
{
    public class NotificationDbContext : DbContext, IUnitOfWork
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }

        public DbSet<Domain.Entities.Notification> Notification => Set<Domain.Entities.Notification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Domain.Entities.Notification>(builder =>
            {
                builder.HasKey(n => n.Id);
                builder.Property(n => n.Id)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);
                builder.Property(n => n.UserId)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);
                builder.Property(n => n.Email)
                    .IsRequired()
                    .HasMaxLength(200)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);
                builder.Property(n => n.IsOnline)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);
                builder.HasIndex(n => n.UserId).IsUnique();
            });
        }
    }
}
