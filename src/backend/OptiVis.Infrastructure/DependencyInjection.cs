using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OptiVis.Application.Interfaces;
using OptiVis.Domain.Interfaces;
using OptiVis.Infrastructure.Persistence;
using OptiVis.Infrastructure.Repositories;
using OptiVis.Infrastructure.Services;

namespace OptiVis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MariaDb")
            ?? "Server=localhost;Database=asteriskcdrdb;User=root;Password=;";

        services.AddDbContext<CdrDbContext>(options =>
        {
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(10, 5, 0)), mysqlOptions =>
            {
                mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                mysqlOptions.CommandTimeout(30);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddScoped<ICallRecordRepository, CallRecordRepository>();
        services.AddSingleton<IOperatorMappingService, OperatorMappingService>();

        return services;
    }
}
