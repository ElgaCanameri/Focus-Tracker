using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Common;
using Notification.Application.Interfaces;
using Notification.Domain.Interfaces;
using Notification.Infrastructure.Repositories;
using Notification.Infrastructure.Services;

namespace Notification.Infrastructure
{
    public static class Startup
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<NotificationDbContext>(options =>
              options.UseSqlServer(
                  configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddSingleton<IOnlineTracker, OnlineTracker>();
            services.AddScoped<IUnitOfWork>(sp =>
               sp.GetRequiredService<NotificationDbContext>());
            return services;
        }
    }
}
