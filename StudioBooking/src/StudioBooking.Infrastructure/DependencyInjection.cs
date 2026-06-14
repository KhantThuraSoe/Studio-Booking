using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;
using StudioBooking.Infrastructure.Auth;
using StudioBooking.Infrastructure.Background;
using StudioBooking.Infrastructure.Persistence;
using StudioBooking.Infrastructure.Persistence.Repositories;
using StudioBooking.Infrastructure.Redis;
using StackExchange.Redis;

namespace StudioBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<ITimetableRepository, TimetableRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<ISlotReservationService, RedisSlotReservationService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<WaitlistCleanupJob>();

        return services;
    }
}
