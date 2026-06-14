using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;

namespace StudioBooking.Application.Services;

public class PackageValidationService : IPackageValidationService
{
    private readonly IPackageRepository _packageRepository;

    public PackageValidationService(IPackageRepository packageRepository)
    {
        _packageRepository = packageRepository;
    }

    public async Task ValidatePackageForScheduleAsync(
        int userId,
        int packageId,
        int scheduleBusinessId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdForUserAsync(packageId, userId, cancellationToken)
            ?? throw new BusinessRuleException("PACKAGE_NOT_FOUND", "Package not found for the current user.");

        if (package.IsExpired(utcNow))
            throw new BusinessRuleException("PACKAGE_EXPIRED", "Package has expired.");

        if (!package.HasAvailableCredits)
            throw new BusinessRuleException("INSUFFICIENT_CREDITS", "Package has no remaining credits.");

        if (package.BusinessId != scheduleBusinessId)
            throw new BusinessRuleException("BUSINESS_MISMATCH", "Package cannot be used for schedules under a different business.");
    }

    public async Task<Package> GetValidatedPackageAsync(
        int userId,
        int packageId,
        int scheduleBusinessId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await ValidatePackageForScheduleAsync(userId, packageId, scheduleBusinessId, utcNow, cancellationToken);
        return (await _packageRepository.GetByIdForUserAsync(packageId, userId, cancellationToken))!;
    }
}
