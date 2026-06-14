using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioBooking.Application.DTOs.Packages;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/packages")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;

    public PackagesController(IPackageService packageService) => _packageService = packageService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PackageDto>>> GetPackages(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var packages = await _packageService.GetAvailablePackagesAsync(userId, cancellationToken);
        return Ok(packages);
    }

    [HttpPost("purchase")]
    public async Task<ActionResult<PurchasePackageResponse>> Purchase(
        [FromBody] PurchasePackageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _packageService.PurchasePackageAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
