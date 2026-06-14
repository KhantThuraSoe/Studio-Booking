using StudioBooking.Application.DTOs.Packages;
using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;

namespace StudioBooking.Application.Services;

public class PackageService : IPackageService
{
    private readonly IPackageRepository _packageRepository;
    private readonly IBusinessRepository _businessRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public PackageService(
        IPackageRepository packageRepository,
        IBusinessRepository businessRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _packageRepository = packageRepository;
        _businessRepository = businessRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<PackageDto>> GetAvailablePackagesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"packages:user:{userId}";
        var cached = await _cacheService.GetAsync<List<PackageDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var utcNow = DateTime.UtcNow;
        var packages = await _packageRepository.GetAvailableByUserIdAsync(userId, utcNow, cancellationToken);

        var result = packages.Select(p => new PackageDto(
            p.Id,
            p.BusinessId,
            p.Business.Name,
            p.TotalCredits,
            p.RemainingCredits,
            p.ExpiryDate,
            p.IsExpired(utcNow))).ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
        return result;
    }

    public async Task<PurchasePackageResponse> PurchasePackageAsync(
        int userId,
        PurchasePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TotalCredits <= 0)
            throw new BusinessRuleException("INVALID_CREDITS", "Total credits must be greater than zero.");

        if (request.ValidityDays <= 0)
            throw new BusinessRuleException("INVALID_VALIDITY", "Validity days must be greater than zero.");

        var business = await _businessRepository.GetByIdAsync(request.BusinessId, cancellationToken)
            ?? throw new BusinessRuleException("BUSINESS_NOT_FOUND", "Business not found.");

        var utcNow = DateTime.UtcNow;
        var package = new Package
        {
            UserId = userId,
            BusinessId = business.Id,
            TotalCredits = request.TotalCredits,
            RemainingCredits = request.TotalCredits,
            PurchasedAt = utcNow,
            ExpiryDate = utcNow.AddDays(request.ValidityDays)
        };

        await _packageRepository.AddAsync(package, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync($"packages:user:{userId}", cancellationToken);

        return new PurchasePackageResponse(package.Id, package.RemainingCredits, package.ExpiryDate);
    }
}
