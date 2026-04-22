using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Session.Application.Common;
using Session.Domain.Interfaces;
using Session.Infrastructure.Repositories;

namespace Session.Infrastructure
{
    public static class Startup
    {
        public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<ISessionShareRepository, SessionShareRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            services.AddScoped<IUnitOfWork>(sp =>
                sp.GetRequiredService<AppDbContext>());

            return services;
        }
    }
}
