using EduFlowAI.Admission.Application.Features.Cycles;
using EduFlowAI.Admission.Application.Features.Offerings;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.Admission.Presentation.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin/cycles")]
public sealed class AdminCyclesController : ControllerBase
{
    private readonly IAdmissionCycleService _cycleService;
    private readonly IOfferingService _offeringService;

    public AdminCyclesController(
        IAdmissionCycleService cycleService,
        IOfferingService offeringService)
    {
        ArgumentNullException.ThrowIfNull(cycleService);
        ArgumentNullException.ThrowIfNull(offeringService);

        _cycleService = cycleService;
        _offeringService = offeringService;
    }

    [HttpGet]
    public async Task<ActionResult<Result<IReadOnlyList<AdmissionCycleDto>>>> GetCycles(
        CancellationToken cancellationToken)
    {
        var cycles = await _cycleService.GetCyclesAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<AdmissionCycleDto>>.Success(cycles));
    }

    [HttpPost]
    public async Task<ActionResult<Result<AdmissionCycleDto>>> CreateCycle(
        [FromBody] CreateAdmissionCycleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cycleService.CreateCycleAsync(
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{cycleId:guid}/eligibility-rule")]
    public async Task<ActionResult<Result<CycleEligibilityRuleDto>>> UpdateEligibilityRule(
        Guid cycleId,
        [FromBody] UpdateCycleEligibilityRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cycleService.UpsertEligibilityRuleAsync(
            cycleId,
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{cycleId:guid}/offerings")]
    public async Task<ActionResult<Result<IReadOnlyList<OfferingDto>>>> ConfigureOfferings(
        Guid cycleId,
        [FromBody] ConfigureOfferingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _offeringService.ConfigureOfferingsAsync(
            cycleId,
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{cycleId:guid}/activate")]
    public async Task<ActionResult<Result<AdmissionCycleDto>>> ActivateCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var result = await _cycleService.ActivateCycleAsync(
            cycleId,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{cycleId:guid}/close")]
    public async Task<ActionResult<Result<AdmissionCycleDto>>> CloseCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var result = await _cycleService.CloseCycleAsync(
            cycleId,
            cancellationToken);

        return this.ToActionResult(result);
    }
}
