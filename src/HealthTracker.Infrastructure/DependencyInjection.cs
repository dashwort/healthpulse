using HealthTracker.Application.Abstractions;
using HealthTracker.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTracker.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            string connectionString
        )
        {
            services.AddDbContext<HealthTrackerDbContext>(options =>
                options.UseSqlite(connectionString)
            );
            services.AddScoped<IHealthDataStore, EfHealthDataStore>();
            services.AddScoped<DatabaseInitializer>();
            return services;
        }
    }
}
