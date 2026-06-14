using Microsoft.Extensions.DependencyInjection;
using StudioBooking.Application.Interfaces;
using StudioBooking.Application.Services;

namespace StudioBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<ITimetableService, TimetableService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<PackageValidationService>();
        services.AddScoped<IWaitlistPromotionService, WaitlistPromotionService>();
        services.AddScoped<IPackageValidationService, PackageValidationService>(sp => sp.GetRequiredService<PackageValidationService>());

        return services;
    }
}
