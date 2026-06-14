using Microsoft.Extensions.DependencyInjection;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Infrastructure.Background;

public class WaitlistCleanupJob
{
    private readonly IServiceProvider _serviceProvider;

    public WaitlistCleanupJob(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var waitlistService = scope.ServiceProvider.GetRequiredService<IWaitlistService>();
        await waitlistService.ExpireEndedWaitlistsAsync(cancellationToken);
    }
}
