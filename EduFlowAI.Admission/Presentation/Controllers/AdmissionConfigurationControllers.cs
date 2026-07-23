using EduFlowAI.Admission.Application.Services.Configuration;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.Admission.Presentation.Controllers;

[ApiController]
[Route("api/tracks")]
public sealed class TracksController : ControllerBase
{
    private readonly IAdmissionConfigurationService _configurationService;

    public TracksController(IAdmissionConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<Result<IReadOnlyList<TrackDto>>>> GetTracks(
        CancellationToken cancellationToken)
    {
        var tracks = await _configurationService.GetPublicTracksAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<TrackDto>>.Success(tracks));
    }

    [HttpGet("{trackId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<TrackDto>>> GetTrack(
        Guid trackId,
        CancellationToken cancellationToken)
    {
        var track = await _configurationService.GetPublicTrackAsync(trackId, cancellationToken);
        return track is null
            ? NotFound(Result<TrackDto>.Failure(
                404,
                "Track was not found in the active admission cycle."))
            : Ok(Result<TrackDto>.Success(track));
    }
}

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin")]
public sealed class AdminAdmissionController : ControllerBase
{
    private readonly IAdmissionConfigurationService _configurationService;

    public AdminAdmissionController(IAdmissionConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpGet("institutions")]
    public async Task<ActionResult<Result<IReadOnlyList<InstitutionDto>>>> GetInstitutions(
        CancellationToken cancellationToken)
    {
        var institutions = await _configurationService.GetInstitutionsAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<InstitutionDto>>.Success(institutions));
    }

    [HttpPost("institutions")]
    public async Task<ActionResult<Result<InstitutionDto>>> CreateInstitution(
        [FromBody] CreateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateInstitutionAsync(request, cancellationToken));
    }

    [HttpPut("institutions/{institutionId:guid}")]
    public async Task<ActionResult<Result<InstitutionDto>>> UpdateInstitution(
        Guid institutionId,
        [FromBody] UpdateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateInstitutionAsync(institutionId, request, cancellationToken));
    }

    [HttpGet("programs")]
    public async Task<ActionResult<Result<IReadOnlyList<ProgramDto>>>> GetPrograms(
        CancellationToken cancellationToken)
    {
        var programs = await _configurationService.GetProgramsAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<ProgramDto>>.Success(programs));
    }

    [HttpPost("programs")]
    public async Task<ActionResult<Result<ProgramDto>>> CreateProgram(
        [FromBody] CreateProgramRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateProgramAsync(request, cancellationToken));
    }

    [HttpPut("programs/{programId:guid}")]
    public async Task<ActionResult<Result<ProgramDto>>> UpdateProgram(
        Guid programId,
        [FromBody] UpdateProgramRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateProgramAsync(programId, request, cancellationToken));
    }

    [HttpGet("programs/{programId:guid}/document-requirements")]
    public async Task<ActionResult<Result<IReadOnlyList<ProgramDocumentRequirementDto>>>> GetProgramDocumentRequirements(
        Guid programId,
        CancellationToken cancellationToken)
    {
        var requirements = await _configurationService.GetProgramRequirementsAsync(programId, cancellationToken);
        return requirements is null
            ? NotFound(Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                404,
                "Program was not found."))
            : Ok(Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Success(requirements));
    }

    [HttpPut("programs/{programId:guid}/document-requirements")]
    public async Task<ActionResult<Result<IReadOnlyList<ProgramDocumentRequirementDto>>>> UpdateProgramDocumentRequirements(
        Guid programId,
        [FromBody] UpdateProgramDocumentRequirementsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.ReplaceProgramRequirementsAsync(programId, request, cancellationToken));
    }

    [HttpGet("tracks")]
    public async Task<ActionResult<Result<IReadOnlyList<TrackDto>>>> GetAdminTracks(
        CancellationToken cancellationToken)
    {
        var tracks = await _configurationService.GetAdminTracksAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<TrackDto>>.Success(tracks));
    }

    [HttpPost("tracks")]
    public async Task<ActionResult<Result<TrackDto>>> CreateTrack(
        [FromBody] CreateTrackRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateTrackAsync(request, cancellationToken));
    }

    [HttpPut("tracks/{trackId:guid}")]
    public async Task<ActionResult<Result<TrackDto>>> UpdateTrack(
        Guid trackId,
        [FromBody] UpdateTrackRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateTrackAsync(trackId, request, cancellationToken));
    }

    [HttpGet("branches")]
    public async Task<ActionResult<Result<IReadOnlyList<BranchDto>>>> GetBranches(
        CancellationToken cancellationToken)
    {
        var branches = await _configurationService.GetBranchesAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<BranchDto>>.Success(branches));
    }

    [HttpPost("branches")]
    public async Task<ActionResult<Result<BranchDto>>> CreateBranch(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateBranchAsync(request, cancellationToken));
    }

    [HttpPut("branches/{branchId:guid}")]
    public async Task<ActionResult<Result<BranchDto>>> UpdateBranch(
        Guid branchId,
        [FromBody] UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateBranchAsync(branchId, request, cancellationToken));
    }

    [HttpGet("cycles")]
    public async Task<ActionResult<Result<IReadOnlyList<AdmissionCycleDto>>>> GetCycles(
        CancellationToken cancellationToken)
    {
        var cycles = await _configurationService.GetCyclesAsync(cancellationToken);
        return Ok(Result<IReadOnlyList<AdmissionCycleDto>>.Success(cycles));
    }

    [HttpPost("cycles")]
    public async Task<ActionResult<Result<AdmissionCycleDto>>> CreateCycle(
        [FromBody] CreateAdmissionCycleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateCycleAsync(request, cancellationToken));
    }

    [HttpPut("cycles/{cycleId:guid}/eligibility-rule")]
    public async Task<ActionResult<Result<CycleEligibilityRuleDto>>> UpdateEligibilityRule(
        Guid cycleId,
        [FromBody] UpdateCycleEligibilityRuleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpsertEligibilityRuleAsync(cycleId, request, cancellationToken));
    }

    [HttpPost("cycles/{cycleId:guid}/offerings")]
    public async Task<ActionResult<Result<IReadOnlyList<OfferingDto>>>> ConfigureOfferings(
        Guid cycleId,
        [FromBody] ConfigureOfferingsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.ConfigureOfferingsAsync(cycleId, request, cancellationToken));
    }

    [HttpPost("cycles/{cycleId:guid}/activate")]
    public async Task<ActionResult<Result<AdmissionCycleDto>>> ActivateCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.ActivateCycleAsync(cycleId, cancellationToken));
    }

    [HttpPost("cycles/{cycleId:guid}/close")]
    public async Task<ActionResult<Result<AdmissionCycleDto>>> CloseCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CloseCycleAsync(cycleId, cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<Result<AdminAdmissionDashboardDto>>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var dashboard = await _configurationService.GetDashboardAsync(cancellationToken);
        return Ok(Result<AdminAdmissionDashboardDto>.Success(dashboard));
    }

    private ActionResult<Result<T>> ToActionResult<T>(Result<T> result)
    {
        return StatusCode(result.StatusCode, result);
    }
}
