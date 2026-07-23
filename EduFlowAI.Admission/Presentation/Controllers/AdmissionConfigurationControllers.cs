using EduFlowAI.Admission.Application.Services.Configuration;
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
    public async Task<ActionResult<IReadOnlyList<TrackDto>>> GetTracks(CancellationToken cancellationToken)
    {
        var tracks = await _configurationService.GetPublicTracksAsync(cancellationToken);
        return Ok(tracks);
    }

    [HttpGet("{trackId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrackDto>> GetTrack(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await _configurationService.GetPublicTrackAsync(trackId, cancellationToken);
        return track is null
            ? NotFound(new { message = "Track was not found in the active admission cycle." })
            : Ok(track);
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
    public async Task<ActionResult<IReadOnlyList<InstitutionDto>>> GetInstitutions(CancellationToken cancellationToken)
    {
        return Ok(await _configurationService.GetInstitutionsAsync(cancellationToken));
    }

    [HttpPost("institutions")]
    public async Task<ActionResult<AdmissionOperationResult<InstitutionDto>>> CreateInstitution(
        [FromBody] CreateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateInstitutionAsync(request, cancellationToken));
    }

    [HttpPut("institutions/{institutionId:guid}")]
    public async Task<ActionResult<AdmissionOperationResult<InstitutionDto>>> UpdateInstitution(
        Guid institutionId,
        [FromBody] UpdateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateInstitutionAsync(institutionId, request, cancellationToken));
    }

    [HttpGet("programs")]
    public async Task<ActionResult<IReadOnlyList<ProgramDto>>> GetPrograms(CancellationToken cancellationToken)
    {
        return Ok(await _configurationService.GetProgramsAsync(cancellationToken));
    }

    [HttpPost("programs")]
    public async Task<ActionResult<AdmissionOperationResult<ProgramDto>>> CreateProgram(
        [FromBody] CreateProgramRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateProgramAsync(request, cancellationToken));
    }

    [HttpPut("programs/{programId:guid}")]
    public async Task<ActionResult<AdmissionOperationResult<ProgramDto>>> UpdateProgram(
        Guid programId,
        [FromBody] UpdateProgramRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateProgramAsync(programId, request, cancellationToken));
    }

    [HttpGet("programs/{programId:guid}/document-requirements")]
    public async Task<ActionResult<IReadOnlyList<ProgramDocumentRequirementDto>>> GetProgramDocumentRequirements(
        Guid programId,
        CancellationToken cancellationToken)
    {
        var requirements = await _configurationService.GetProgramRequirementsAsync(programId, cancellationToken);
        return requirements is null
            ? NotFound(new { message = "Program was not found." })
            : Ok(requirements);
    }

    [HttpPut("programs/{programId:guid}/document-requirements")]
    public async Task<ActionResult<AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>>> UpdateProgramDocumentRequirements(
        Guid programId,
        [FromBody] UpdateProgramDocumentRequirementsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.ReplaceProgramRequirementsAsync(programId, request, cancellationToken));
    }

    [HttpGet("tracks")]
    public async Task<ActionResult<IReadOnlyList<TrackDto>>> GetAdminTracks(CancellationToken cancellationToken)
    {
        return Ok(await _configurationService.GetAdminTracksAsync(cancellationToken));
    }

    [HttpPost("tracks")]
    public async Task<ActionResult<AdmissionOperationResult<TrackDto>>> CreateTrack(
        [FromBody] CreateTrackRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateTrackAsync(request, cancellationToken));
    }

    [HttpPut("tracks/{trackId:guid}")]
    public async Task<ActionResult<AdmissionOperationResult<TrackDto>>> UpdateTrack(
        Guid trackId,
        [FromBody] UpdateTrackRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateTrackAsync(trackId, request, cancellationToken));
    }

    [HttpGet("branches")]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches(CancellationToken cancellationToken)
    {
        return Ok(await _configurationService.GetBranchesAsync(cancellationToken));
    }

    [HttpPost("branches")]
    public async Task<ActionResult<AdmissionOperationResult<BranchDto>>> CreateBranch(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateBranchAsync(request, cancellationToken));
    }

    [HttpPut("branches/{branchId:guid}")]
    public async Task<ActionResult<AdmissionOperationResult<BranchDto>>> UpdateBranch(
        Guid branchId,
        [FromBody] UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpdateBranchAsync(branchId, request, cancellationToken));
    }

    [HttpGet("cycles")]
    public async Task<ActionResult<IReadOnlyList<AdmissionCycleDto>>> GetCycles(CancellationToken cancellationToken)
    {
        return Ok(await _configurationService.GetCyclesAsync(cancellationToken));
    }

    [HttpPost("cycles")]
    public async Task<ActionResult<AdmissionOperationResult<AdmissionCycleDto>>> CreateCycle(
        [FromBody] CreateAdmissionCycleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CreateCycleAsync(request, cancellationToken));
    }

    [HttpPut("cycles/{cycleId:guid}/eligibility-rule")]
    public async Task<ActionResult<AdmissionOperationResult<CycleEligibilityRuleDto>>> UpdateEligibilityRule(
        Guid cycleId,
        [FromBody] UpdateCycleEligibilityRuleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.UpsertEligibilityRuleAsync(cycleId, request, cancellationToken));
    }

    [HttpPost("cycles/{cycleId:guid}/offerings")]
    public async Task<ActionResult<AdmissionOperationResult<IReadOnlyList<OfferingDto>>>> ConfigureOfferings(
        Guid cycleId,
        [FromBody] ConfigureOfferingsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.ConfigureOfferingsAsync(cycleId, request, cancellationToken));
    }

    [HttpPost("cycles/{cycleId:guid}/activate")]
    public async Task<ActionResult<AdmissionOperationResult<AdmissionCycleDto>>> ActivateCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.ActivateCycleAsync(cycleId, cancellationToken));
    }

    [HttpPost("cycles/{cycleId:guid}/close")]
    public async Task<ActionResult<AdmissionOperationResult<AdmissionCycleDto>>> CloseCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _configurationService.CloseCycleAsync(cycleId, cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminAdmissionDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        return Ok(await _configurationService.GetDashboardAsync(cancellationToken));
    }

    private ActionResult<AdmissionOperationResult<T>> ToActionResult<T>(AdmissionOperationResult<T> result)
    {
        return StatusCode(result.StatusCode, result);
    }
}
