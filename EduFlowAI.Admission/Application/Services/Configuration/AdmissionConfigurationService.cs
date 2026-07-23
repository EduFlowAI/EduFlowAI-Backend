using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace EduFlowAI.Admission.Application.Services.Configuration;

public sealed class InstitutionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int ProgramCount { get; init; }
}

public sealed class ProgramDto
{
    public Guid Id { get; init; }
    public Guid InstitutionId { get; init; }
    public string InstitutionName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int DurationMonths { get; init; }
    public int TrackCount { get; init; }
    public int CycleCount { get; init; }
}

public sealed class ProgramDocumentRequirementDto
{
    public Guid Id { get; init; }
    public Guid ProgramId { get; init; }
    public DocumentType DocumentType { get; init; }
    public Gender? RequiredForGender { get; init; }
}

public sealed class BranchOfferingDto
{
    public Guid OfferingId { get; init; }
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? Governorate { get; init; }
    public int Capacity { get; init; }
}

public sealed class TrackDto
{
    public Guid Id { get; init; }
    public Guid ProgramId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> PrerequisiteTopics { get; init; } = Array.Empty<string>();
    public bool IsActive { get; init; }
    public IReadOnlyList<BranchOfferingDto> Offerings { get; init; } = Array.Empty<BranchOfferingDto>();
}

public sealed class BranchDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Governorate { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CycleEligibilityRuleDto
{
    public Guid Id { get; init; }
    public Guid CycleId { get; init; }
    public string RequiredNationality { get; init; } = string.Empty;
    public string RequiredDegreeLevel { get; init; } = string.Empty;
    public int MaxYearsSinceGraduation { get; init; }
    public CumulativeGrade MinGrade { get; init; }
}

public sealed class OfferingDto
{
    public Guid Id { get; init; }
    public Guid CycleId { get; init; }
    public Guid TrackId { get; init; }
    public string TrackName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public int Capacity { get; init; }
}

public sealed class AdmissionCycleDto
{
    public Guid Id { get; init; }
    public Guid ProgramId { get; init; }
    public string ProgramName { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateTimeOffset DeadlineUtc { get; init; }
    public CycleStatus Status { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public uint RowVersion { get; init; }
    public CycleEligibilityRuleDto? EligibilityRule { get; init; }
    public IReadOnlyList<OfferingDto> Offerings { get; init; } = Array.Empty<OfferingDto>();
}

public sealed class AdminAdmissionDashboardDto
{
    public int InstitutionCount { get; init; }
    public int ProgramCount { get; init; }
    public int ActiveTrackCount { get; init; }
    public int ActiveBranchCount { get; init; }
    public int DraftCycleCount { get; init; }
    public int ClosedCycleCount { get; init; }
    public int ApplicationCount { get; init; }
    public AdmissionCycleDto? ActiveCycle { get; init; }
    public int ActiveCycleOfferingCount { get; init; }
    public int ActiveCycleCapacity { get; init; }
}

public sealed class CreateInstitutionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; init; } = string.Empty;
}

public sealed class UpdateInstitutionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; init; } = string.Empty;
}

public sealed class CreateProgramRequest
{
    public Guid InstitutionId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(30)]
    public string Code { get; init; } = string.Empty;

    [Range(1, 60)]
    public int DurationMonths { get; init; }
}

public sealed class UpdateProgramRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(30)]
    public string Code { get; init; } = string.Empty;

    [Range(1, 60)]
    public int DurationMonths { get; init; }
}

public sealed class ProgramDocumentRequirementInput
{
    public DocumentType DocumentType { get; init; }
    public Gender? RequiredForGender { get; init; }
}

public sealed class UpdateProgramDocumentRequirementsRequest
{
    [Required, MaxLength(12)]
    public ProgramDocumentRequirementInput[] Requirements { get; init; } = [];
}

public sealed class CreateTrackRequest
{
    public Guid ProgramId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(50)]
    public string[] PrerequisiteTopics { get; init; } = [];
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateTrackRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(50)]
    public string[] PrerequisiteTopics { get; init; } = [];
    public bool IsActive { get; init; } = true;
}

public sealed class CreateBranchRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Governorate { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class UpdateBranchRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Governorate { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class CreateAdmissionCycleRequest
{
    public Guid ProgramId { get; init; }

    [Required, MaxLength(200)]
    public string Label { get; init; } = string.Empty;

    public DateOnly StartDate { get; init; }
    public DateTimeOffset DeadlineUtc { get; init; }
}

public sealed class UpdateCycleEligibilityRuleRequest
{
    [Required, MaxLength(5)]
    public string RequiredNationality { get; init; } = "EG";

    [Required, MaxLength(30)]
    public string RequiredDegreeLevel { get; init; } = "Bachelor";

    [Range(0, 100)]
    public int MaxYearsSinceGraduation { get; init; } = 5;

    public CumulativeGrade MinGrade { get; init; } = CumulativeGrade.Good;
}

public sealed class OfferingInput
{
    public Guid TrackId { get; init; }
    public Guid BranchId { get; init; }

    [Range(1, int.MaxValue)]
    public int Capacity { get; init; }
}

public sealed class ConfigureOfferingsRequest
{
    [Required, MaxLength(500)]
    public OfferingInput[] Offerings { get; init; } = [];
}

public interface IAdmissionConfigurationService
{
    Task<IReadOnlyList<TrackDto>> GetPublicTracksAsync(CancellationToken cancellationToken = default);
    Task<TrackDto?> GetPublicTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default);
    Task<Result<InstitutionDto>> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default);
    Task<Result<InstitutionDto>> UpdateInstitutionAsync(Guid institutionId, UpdateInstitutionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramDto>> GetProgramsAsync(CancellationToken cancellationToken = default);
    Task<Result<ProgramDto>> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProgramDto>> UpdateProgramAsync(Guid programId, UpdateProgramRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProgramDocumentRequirementDto>?> GetProgramRequirementsAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProgramDocumentRequirementDto>>> ReplaceProgramRequirementsAsync(Guid programId, UpdateProgramDocumentRequirementsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(CancellationToken cancellationToken = default);
    Task<Result<TrackDto>> CreateTrackAsync(CreateTrackRequest request, CancellationToken cancellationToken = default);
    Task<Result<TrackDto>> UpdateTrackAsync(Guid trackId, UpdateTrackRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<Result<BranchDto>> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<Result<BranchDto>> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdmissionCycleDto>> GetCyclesAsync(CancellationToken cancellationToken = default);
    Task<Result<AdmissionCycleDto>> CreateCycleAsync(CreateAdmissionCycleRequest request, CancellationToken cancellationToken = default);
    Task<Result<CycleEligibilityRuleDto>> UpsertEligibilityRuleAsync(Guid cycleId, UpdateCycleEligibilityRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OfferingDto>>> ConfigureOfferingsAsync(Guid cycleId, ConfigureOfferingsRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdmissionCycleDto>> ActivateCycleAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<Result<AdmissionCycleDto>> CloseCycleAsync(Guid cycleId, CancellationToken cancellationToken = default);

    Task<AdminAdmissionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public sealed class AdmissionConfigurationService : IAdmissionConfigurationService
{
    private readonly IAdmissionDbContext _dbContext;
    private readonly IAdmissionApplicationReader _applicationReader;
    private readonly TimeProvider _timeProvider;

    public AdmissionConfigurationService(
        IAdmissionDbContext dbContext,
        IAdmissionApplicationReader applicationReader,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(applicationReader);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _applicationReader = applicationReader;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<TrackDto>> GetPublicTracksAsync(CancellationToken cancellationToken = default)
    {
        var offerings = await _dbContext.TrackBranchOfferings
            .AsNoTracking()
            .Include(x => x.Track)
            .Include(x => x.Branch)
            .Where(x =>
                x.Cycle.Status == CycleStatus.Active &&
                x.Track.IsActive &&
                x.Branch.IsActive)
            .OrderBy(x => x.Track.Name)
            .ThenBy(x => x.Branch.Name)
            .ToListAsync(cancellationToken);

        return offerings
            .GroupBy(x => x.TrackId)
            .Select(group => MapTrack(group.First().Track, group.Select(MapBranchOffering).ToList()))
            .ToList();
    }

    public async Task<TrackDto?> GetPublicTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        var offerings = await _dbContext.TrackBranchOfferings
            .AsNoTracking()
            .Include(x => x.Track)
            .Include(x => x.Branch)
            .Where(x =>
                x.TrackId == trackId &&
                x.Cycle.Status == CycleStatus.Active &&
                x.Track.IsActive &&
                x.Branch.IsActive)
            .OrderBy(x => x.Branch.Name)
            .ToListAsync(cancellationToken);

        if (offerings.Count == 0)
        {
            return null;
        }

        return MapTrack(offerings[0].Track, offerings.Select(MapBranchOffering).ToList());
    }

    public async Task<IReadOnlyList<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Institutions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new InstitutionDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ProgramCount = x.Programs.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<InstitutionDto>> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<InstitutionDto>.Failure(400, "Request body is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string code = NormalizeCode(request.Code);
        if (!IsRequiredTextValid(name, 200) || !IsRequiredTextValid(code, 20))
        {
            return Result<InstitutionDto>.Failure(
                400,
                "Institution name is required and must not exceed 200 characters; code is required and must not exceed 20 characters.");
        }

        return await ExecuteWriteAsync(
            cancellationToken,
            async ct =>
            {
                if (await _dbContext.Institutions.AnyAsync(x => x.Code == code, ct))
                {
                    return Result<InstitutionDto>.Failure(409, $"Institution code '{code}' already exists.");
                }

                var institution = new Institution
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Code = code,
                    CreatedAt = UtcNow
                };

                _dbContext.Institutions.Add(institution);
                await _dbContext.SaveChangesAsync(ct);

                return Result<InstitutionDto>.Success(
                    MapInstitution(institution, 0),
                    201,
                    "Institution created successfully.");
            },
            ("UX_Institutions_Code", $"Institution code '{code}' already exists."));
    }

    public async Task<Result<InstitutionDto>> UpdateInstitutionAsync(Guid institutionId, UpdateInstitutionRequest request, CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty)
        {
            return Result<InstitutionDto>.Failure(400, "A valid institution ID is required.");
        }

        if (request is null)
        {
            return Result<InstitutionDto>.Failure(400, "Request body is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string code = NormalizeCode(request.Code);
        if (!IsRequiredTextValid(name, 200) || !IsRequiredTextValid(code, 20))
        {
            return Result<InstitutionDto>.Failure(
                400,
                "Institution name is required and must not exceed 200 characters; code is required and must not exceed 20 characters.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var institution = await _dbContext.Institutions
                    .SingleOrDefaultAsync(x => x.Id == institutionId, ct);

                if (institution is null)
                {
                    return Result<InstitutionDto>.Failure(404, "Institution was not found.");
                }

                if (await _applicationReader.IsInstitutionConfigurationLockedAsync(institutionId, ct))
                {
                    return Result<InstitutionDto>.Failure(
                        409,
                        "Institution configuration is locked because one of its admission cycles is Active or already has applications.");
                }

                if (await _dbContext.Institutions.AnyAsync(
                        x => x.Id != institutionId && x.Code == code,
                        ct))
                {
                    return Result<InstitutionDto>.Failure(409, $"Institution code '{code}' already exists.");
                }

                institution.Name = name;
                institution.Code = code;
                await _dbContext.SaveChangesAsync(ct);

                int programCount = await _dbContext.Programs.CountAsync(
                    x => x.InstitutionId == institutionId,
                    ct);

                return Result<InstitutionDto>.Success(
                    MapInstitution(institution, programCount),
                    message: "Institution updated successfully.");
            },
            ("UX_Institutions_Code", $"Institution code '{code}' already exists."));
    }

    public async Task<IReadOnlyList<ProgramDto>> GetProgramsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Programs
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProgramDto
            {
                Id = x.Id,
                InstitutionId = x.InstitutionId,
                InstitutionName = x.Institution.Name,
                Name = x.Name,
                Code = x.Code,
                DurationMonths = x.DurationMonths,
                TrackCount = x.Tracks.Count,
                CycleCount = x.AdmissionCycles.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<ProgramDto>> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<ProgramDto>.Failure(400, "Request body is required.");
        }

        if (request.InstitutionId == Guid.Empty)
        {
            return Result<ProgramDto>.Failure(400, "A valid institution ID is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string code = NormalizeCode(request.Code);
        if (!IsRequiredTextValid(name, 200) ||
            !IsRequiredTextValid(code, 30) ||
            request.DurationMonths is < 1 or > 60)
        {
            return Result<ProgramDto>.Failure(
                400,
                "Program name and code are required, and duration must be between 1 and 60 months.");
        }

        var institution = await _dbContext.Institutions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.InstitutionId, cancellationToken);

        if (institution is null)
        {
            return Result<ProgramDto>.Failure(404, "Institution was not found.");
        }

        return await ExecuteWriteAsync(
            cancellationToken,
            async ct =>
            {
                if (await _dbContext.Programs.AnyAsync(x => x.Code == code, ct))
                {
                    return Result<ProgramDto>.Failure(409, $"Program code '{code}' already exists.");
                }

                var program = new Program
                {
                    Id = Guid.NewGuid(),
                    InstitutionId = request.InstitutionId,
                    Name = name,
                    Code = code,
                    DurationMonths = request.DurationMonths,
                    CreatedAt = UtcNow
                };

                _dbContext.Programs.Add(program);
                await _dbContext.SaveChangesAsync(ct);

                return Result<ProgramDto>.Success(
                    MapProgram(program, institution.Name, 0, 0),
                    statusCode: 201,
                    message: "Program created successfully.");
            },
            ("UX_Programs_Code", $"Program code '{code}' already exists."));
    }

    public async Task<Result<ProgramDto>> UpdateProgramAsync(Guid programId, UpdateProgramRequest request, CancellationToken cancellationToken = default)
    {
        if (programId == Guid.Empty)
        {
            return Result<ProgramDto>.Failure(400, "A valid program ID is required.");
        }

        if (request is null)
        {
            return Result<ProgramDto>.Failure(400, "Request body is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string code = NormalizeCode(request.Code);
        if (!IsRequiredTextValid(name, 200) ||
            !IsRequiredTextValid(code, 30) ||
            request.DurationMonths is < 1 or > 60)
        {
            return Result<ProgramDto>.Failure(
                400,
                "Program name and code are required, and duration must be between 1 and 60 months.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var program = await _dbContext.Programs
                    .Include(x => x.Institution)
                    .SingleOrDefaultAsync(x => x.Id == programId, ct);

                if (program is null)
                {
                    return Result<ProgramDto>.Failure(404, "Program was not found.");
                }

                if (await _applicationReader.IsProgramConfigurationLockedAsync(programId, ct))
                {
                    return Result<ProgramDto>.Failure(
                        409,
                        "Program configuration is locked because one of its cycles is Active or already has applications.");
                }

                if (await _dbContext.Programs.AnyAsync(
                        x => x.Id != programId && x.Code == code,
                        ct))
                {
                    return Result<ProgramDto>.Failure(409, $"Program code '{code}' already exists.");
                }

                program.Name = name;
                program.Code = code;
                program.DurationMonths = request.DurationMonths;
                await _dbContext.SaveChangesAsync(ct);

                int trackCount = await _dbContext.Tracks.CountAsync(
                    x => x.ProgramId == programId,
                    ct);
                int cycleCount = await _dbContext.AdmissionCycles.CountAsync(
                    x => x.ProgramId == programId,
                    ct);

                return Result<ProgramDto>.Success(
                    MapProgram(program, program.Institution.Name, trackCount, cycleCount),
                    message: "Program updated successfully.");
            },
            ("UX_Programs_Code", $"Program code '{code}' already exists."));
    }

    public async Task<IReadOnlyList<ProgramDocumentRequirementDto>?> GetProgramRequirementsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.Programs.AnyAsync(x => x.Id == programId, cancellationToken))
        {
            return null;
        }

        return await _dbContext.ProgramDocumentRequirements
            .AsNoTracking()
            .Where(x => x.ProgramId == programId)
            .OrderBy(x => x.DocumentType)
            .ThenBy(x => x.RequiredForGender)
            .Select(x => new ProgramDocumentRequirementDto
            {
                Id = x.Id,
                ProgramId = x.ProgramId,
                DocumentType = x.DocumentType,
                RequiredForGender = x.RequiredForGender
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ProgramDocumentRequirementDto>>> ReplaceProgramRequirementsAsync(Guid programId, UpdateProgramDocumentRequirementsRequest request, CancellationToken cancellationToken = default)
    {
        if (programId == Guid.Empty)
        {
            return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                400,
                "A valid program ID is required.");
        }

        if (request?.Requirements is null)
        {
            return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                400,
                "A requirements list is required.");
        }

        if (request.Requirements.Length == 0)
        {
            return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(400, "At least one document requirement is required.");
        }

        if (request.Requirements.Length > 12)
        {
            return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                400,
                "A program can contain at most 12 document requirement combinations.");
        }

        if (request.Requirements.Any(input =>
                input is null ||
                !Enum.IsDefined(input.DocumentType) ||
                input.DocumentType == DocumentType.None ||
                (input.RequiredForGender.HasValue &&
                 (!Enum.IsDefined(input.RequiredForGender.Value) ||
                  input.RequiredForGender.Value == Gender.None))))
        {
            return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                400,
                "Every document type must be defined; gender must be Male, Female, or null for all genders.");
        }

        bool hasDuplicates = request.Requirements
            .GroupBy(x => new { x.DocumentType, x.RequiredForGender })
            .Any(group => group.Count() > 1);

        if (hasDuplicates)
        {
            return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(400, "Duplicate document requirement combinations are not allowed.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                if (!await _dbContext.Programs.AnyAsync(x => x.Id == programId, ct))
                {
                    return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                        404,
                        "Program was not found.");
                }

                if (await _applicationReader.IsProgramConfigurationLockedAsync(programId, ct))
                {
                    return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                        409,
                        "Document requirements are locked because the program has an Active cycle or already has applications.");
                }

                var existing = await _dbContext.ProgramDocumentRequirements
                    .Where(x => x.ProgramId == programId)
                    .ToListAsync(ct);

                if (existing
                    .GroupBy(requirement => new RequirementKey(
                        requirement.DocumentType,
                        requirement.RequiredForGender))
                    .Any(group => group.Count() > 1))
                {
                    return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(
                        409,
                        "Existing document requirements contain duplicate keys. Resolve the database duplicates before updating this program.");
                }

                var existingByKey = existing.ToDictionary(
                    requirement => new RequirementKey(
                        requirement.DocumentType,
                        requirement.RequiredForGender));

                var requestedKeys = request.Requirements
                    .Select(input => new RequirementKey(
                        input.DocumentType,
                        input.RequiredForGender))
                    .ToHashSet();

                var removed = existing
                    .Where(requirement =>
                        !requestedKeys.Contains(new RequirementKey(
                            requirement.DocumentType,
                            requirement.RequiredForGender)))
                    .ToList();

                _dbContext.ProgramDocumentRequirements.RemoveRange(removed);

                var effectiveRequirements = new List<ProgramDocumentRequirement>(
                    request.Requirements.Length);

                foreach (var input in request.Requirements)
                {
                    var key = new RequirementKey(
                        input.DocumentType,
                        input.RequiredForGender);

                    if (existingByKey.TryGetValue(key, out var retained))
                    {
                        effectiveRequirements.Add(retained);
                        continue;
                    }

                    var added = new ProgramDocumentRequirement
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programId,
                        DocumentType = input.DocumentType,
                        RequiredForGender = input.RequiredForGender,
                        CreatedAt = UtcNow,
                        UpdatedAt = UtcNow
                    };

                    _dbContext.ProgramDocumentRequirements.Add(added);
                    effectiveRequirements.Add(added);
                }

                await _dbContext.SaveChangesAsync(ct);

                IReadOnlyList<ProgramDocumentRequirementDto> data =
                    effectiveRequirements
                        .OrderBy(x => x.DocumentType)
                        .ThenBy(x => x.RequiredForGender)
                        .Select(MapRequirement)
                        .ToList();

                return Result<IReadOnlyList<ProgramDocumentRequirementDto>>.Success(
                    data,
                    message: "Program document requirements updated successfully.");
            },
            (
                "UX_ProgramDocumentRequirements_AllGenders",
                "A duplicate all-genders document requirement already exists."),
            (
                "UX_ProgramDocumentRequirements_GenderScoped",
                "A duplicate gender-specific document requirement already exists."));
    }

    public async Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(CancellationToken cancellationToken = default)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return tracks.Select(x => MapTrack(x, Array.Empty<BranchOfferingDto>())).ToList();
    }

    public async Task<Result<TrackDto>> CreateTrackAsync(CreateTrackRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<TrackDto>.Failure(400, "Request body is required.");
        }

        if (request.ProgramId == Guid.Empty)
        {
            return Result<TrackDto>.Failure(400, "A valid program ID is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string? description = NormalizeOptionalText(request.Description);
        if (!IsRequiredTextValid(name, 200) || description?.Length > 4000)
        {
            return Result<TrackDto>.Failure(
                400,
                "Track name is required and must not exceed 200 characters; description must not exceed 4000 characters.");
        }

        var topicValidation = ValidateAndNormalizeTopics(request.PrerequisiteTopics);
        if (!topicValidation.IsValid)
        {
            return Result<TrackDto>.Failure(400, topicValidation.ErrorMessage);
        }

        return await ExecuteWriteAsync(
            cancellationToken,
            async ct =>
            {
                if (!await _dbContext.Programs.AnyAsync(x => x.Id == request.ProgramId, ct))
                {
                    return Result<TrackDto>.Failure(404, "Program was not found.");
                }

                if (await _dbContext.Tracks.AnyAsync(
                        x => x.ProgramId == request.ProgramId && x.Name == name,
                        ct))
                {
                    return Result<TrackDto>.Failure(
                        409,
                        "A track with the same name already exists in this program.");
                }

                var track = new Track
                {
                    Id = Guid.NewGuid(),
                    ProgramId = request.ProgramId,
                    Name = name,
                    Description = description,
                    PrerequisiteTopics = topicValidation.Topics,
                    IsActive = request.IsActive,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                };

                _dbContext.Tracks.Add(track);
                await _dbContext.SaveChangesAsync(ct);

                return Result<TrackDto>.Success(
                    MapTrack(track, Array.Empty<BranchOfferingDto>()),
                    statusCode: 201,
                    message: "Track created successfully.");
            },
            (
                "UX_Tracks_Program_Name",
                "A track with the same name already exists in this program."));
    }

    public async Task<Result<TrackDto>> UpdateTrackAsync(Guid trackId, UpdateTrackRequest request, CancellationToken cancellationToken = default)
    {
        if (trackId == Guid.Empty)
        {
            return Result<TrackDto>.Failure(400, "A valid track ID is required.");
        }

        if (request is null)
        {
            return Result<TrackDto>.Failure(400, "Request body is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string? description = NormalizeOptionalText(request.Description);
        if (!IsRequiredTextValid(name, 200) || description?.Length > 4000)
        {
            return Result<TrackDto>.Failure(
                400,
                "Track name is required and must not exceed 200 characters; description must not exceed 4000 characters.");
        }

        var topicValidation = ValidateAndNormalizeTopics(request.PrerequisiteTopics);
        if (!topicValidation.IsValid)
        {
            return Result<TrackDto>.Failure(400, topicValidation.ErrorMessage);
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var track = await _dbContext.Tracks.SingleOrDefaultAsync(
                    x => x.Id == trackId,
                    ct);
                if (track is null)
                {
                    return Result<TrackDto>.Failure(404, "Track was not found.");
                }

                if (await _applicationReader.IsTrackConfigurationLockedAsync(trackId, ct))
                {
                    return Result<TrackDto>.Failure(
                        409,
                        "Track configuration is locked because it belongs to an Active cycle or a cycle that already has applications.");
                }

                if (await _dbContext.Tracks.AnyAsync(
                        x =>
                            x.Id != trackId &&
                            x.ProgramId == track.ProgramId &&
                            x.Name == name,
                        ct))
                {
                    return Result<TrackDto>.Failure(
                        409,
                        "A track with the same name already exists in this program.");
                }

                track.Name = name;
                track.Description = description;
                track.PrerequisiteTopics = topicValidation.Topics;
                track.IsActive = request.IsActive;
                track.UpdatedAt = UtcNow;

                await _dbContext.SaveChangesAsync(ct);
                return Result<TrackDto>.Success(
                    MapTrack(track, Array.Empty<BranchOfferingDto>()),
                    message: "Track updated successfully.");
            },
            (
                "UX_Tracks_Program_Name",
                "A track with the same name already exists in this program."));
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Branches
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BranchDto
            {
                Id = x.Id,
                Name = x.Name,
                Governorate = x.Governorate,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<BranchDto>> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<BranchDto>.Failure(400, "Request body is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string? governorate = NormalizeOptionalText(request.Governorate);
        if (!IsRequiredTextValid(name, 200) || governorate?.Length > 100)
        {
            return Result<BranchDto>.Failure(
                400,
                "Branch name is required and must not exceed 200 characters; governorate must not exceed 100 characters.");
        }

        return await ExecuteWriteAsync(
            cancellationToken,
            async ct =>
            {
                if (await _dbContext.Branches.AnyAsync(x => x.Name == name, ct))
                {
                    return Result<BranchDto>.Failure(
                        409,
                        "A branch with the same name already exists.");
                }

                var branch = new Branch
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Governorate = governorate,
                    IsActive = request.IsActive,
                    CreatedAt = UtcNow
                };

                _dbContext.Branches.Add(branch);
                await _dbContext.SaveChangesAsync(ct);

                return Result<BranchDto>.Success(
                    MapBranch(branch),
                    statusCode: 201,
                    message: "Branch created successfully.");
            },
            ("UX_Branches_Name", "A branch with the same name already exists."));
    }

    public async Task<Result<BranchDto>> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        if (branchId == Guid.Empty)
        {
            return Result<BranchDto>.Failure(400, "A valid branch ID is required.");
        }

        if (request is null)
        {
            return Result<BranchDto>.Failure(400, "Request body is required.");
        }

        string name = NormalizeRequiredText(request.Name);
        string? governorate = NormalizeOptionalText(request.Governorate);
        if (!IsRequiredTextValid(name, 200) || governorate?.Length > 100)
        {
            return Result<BranchDto>.Failure(
                400,
                "Branch name is required and must not exceed 200 characters; governorate must not exceed 100 characters.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var branch = await _dbContext.Branches.SingleOrDefaultAsync(
                    x => x.Id == branchId,
                    ct);
                if (branch is null)
                {
                    return Result<BranchDto>.Failure(404, "Branch was not found.");
                }

                if (await _applicationReader.IsBranchConfigurationLockedAsync(branchId, ct))
                {
                    return Result<BranchDto>.Failure(
                        409,
                        "Branch configuration is locked because it belongs to an Active cycle or a cycle that already has applications.");
                }

                if (await _dbContext.Branches.AnyAsync(
                        x => x.Id != branchId && x.Name == name,
                        ct))
                {
                    return Result<BranchDto>.Failure(
                        409,
                        "A branch with the same name already exists.");
                }

                branch.Name = name;
                branch.Governorate = governorate;
                branch.IsActive = request.IsActive;
                await _dbContext.SaveChangesAsync(ct);

                return Result<BranchDto>.Success(
                    MapBranch(branch),
                    message: "Branch updated successfully.");
            },
            ("UX_Branches_Name", "A branch with the same name already exists."));
    }

    public async Task<IReadOnlyList<AdmissionCycleDto>> GetCyclesAsync(CancellationToken cancellationToken = default)
    {
        var cycles = await _dbContext.AdmissionCycles
            .AsNoTracking()
            .Include(x => x.Program)
            .Include(x => x.EligibilityRule)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Track)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Branch)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return cycles.Select(MapCycle).ToList();
    }

    public async Task<Result<AdmissionCycleDto>> CreateCycleAsync(CreateAdmissionCycleRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<AdmissionCycleDto>.Failure(400, "Request body is required.");
        }

        if (request.ProgramId == Guid.Empty)
        {
            return Result<AdmissionCycleDto>.Failure(400, "A valid program ID is required.");
        }

        if (request.StartDate == default)
        {
            return Result<AdmissionCycleDto>.Failure(400, "StartDate is required.");
        }

        string label = NormalizeRequiredText(request.Label);
        if (!IsRequiredTextValid(label, 200))
        {
            return Result<AdmissionCycleDto>.Failure(
                400,
                "Cycle label is required and must not exceed 200 characters.");
        }

        DateTimeOffset deadlineUtc = request.DeadlineUtc.ToUniversalTime();
        if (deadlineUtc <= UtcNow)
        {
            return Result<AdmissionCycleDto>.Failure(400, "The application deadline must be in the future.");
        }

        if (request.StartDate > DateOnly.FromDateTime(deadlineUtc.UtcDateTime))
        {
            return Result<AdmissionCycleDto>.Failure(400, "StartDate cannot be after the application deadline.");
        }

        return await ExecuteWriteAsync(
            cancellationToken,
            async ct =>
            {
                var program = await _dbContext.Programs
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == request.ProgramId, ct);

                if (program is null)
                {
                    return Result<AdmissionCycleDto>.Failure(404, "Program was not found.");
                }

                if (await _dbContext.AdmissionCycles.AnyAsync(
                        x => x.ProgramId == request.ProgramId && x.Label == label,
                        ct))
                {
                    return Result<AdmissionCycleDto>.Failure(
                        409,
                        "A cycle with the same label already exists for this program.");
                }

                var cycle = new AdmissionCycle
                {
                    Id = Guid.NewGuid(),
                    ProgramId = request.ProgramId,
                    Label = label,
                    StartDate = request.StartDate,
                    DeadlineUtc = deadlineUtc,
                    Status = CycleStatus.Draft,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                };

                _dbContext.AdmissionCycles.Add(cycle);
                await _dbContext.SaveChangesAsync(ct);

                return Result<AdmissionCycleDto>.Success(
                    MapCycle(cycle, program.Name),
                    statusCode: 201,
                    message: "Admission cycle created as Draft.");
            },
            (
                "UX_AdmissionCycles_Program_Label",
                "A cycle with the same label already exists for this program."));
    }

    public async Task<Result<CycleEligibilityRuleDto>> UpsertEligibilityRuleAsync(Guid cycleId, UpdateCycleEligibilityRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (cycleId == Guid.Empty)
        {
            return Result<CycleEligibilityRuleDto>.Failure(400, "A valid cycle ID is required.");
        }

        if (request is null)
        {
            return Result<CycleEligibilityRuleDto>.Failure(400, "Request body is required.");
        }

        if (!Enum.IsDefined(request.MinGrade) ||
            request.MinGrade == CumulativeGrade.None)
        {
            return Result<CycleEligibilityRuleDto>.Failure(
                400,
                "Minimum grade must be Acceptable, Good, VeryGood, or Excellent.");
        }

        string nationality = NormalizeCode(request.RequiredNationality);
        string degreeLevel = NormalizeRequiredText(request.RequiredDegreeLevel);
        if (!IsRequiredTextValid(nationality, 5) ||
            !IsRequiredTextValid(degreeLevel, 30) ||
            request.MaxYearsSinceGraduation is < 0 or > 100)
        {
            return Result<CycleEligibilityRuleDto>.Failure(
                400,
                "Nationality and degree level are required, and graduation recency must be between 0 and 100 years.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var cycle = await _dbContext.AdmissionCycles.SingleOrDefaultAsync(
                    x => x.Id == cycleId,
                    ct);
                if (cycle is null)
                {
                    return Result<CycleEligibilityRuleDto>.Failure(
                        404,
                        "Admission cycle was not found.");
                }

                var configurationError = await ValidateCycleIsConfigurableAsync(cycle, ct);
                if (configurationError is not null)
                {
                    return Result<CycleEligibilityRuleDto>.Failure(409, configurationError);
                }

                var rule = await _dbContext.CycleEligibilityRules
                    .SingleOrDefaultAsync(x => x.CycleId == cycleId, ct);

                var now = UtcNow;
                if (rule is null)
                {
                    rule = new CycleEligibilityRule
                    {
                        Id = Guid.NewGuid(),
                        CycleId = cycleId,
                        CreatedAt = now
                    };
                    _dbContext.CycleEligibilityRules.Add(rule);
                }

                rule.RequiredNationality = nationality;
                rule.RequiredDegreeLevel = degreeLevel;
                rule.MaxYearsSinceGraduation = request.MaxYearsSinceGraduation;
                rule.MinGrade = request.MinGrade;
                rule.UpdatedAt = now;
                cycle.UpdatedAt = now;

                await _dbContext.SaveChangesAsync(ct);
                return Result<CycleEligibilityRuleDto>.Success(
                    MapEligibilityRule(rule),
                    message: "Cycle eligibility rule saved successfully.");
            },
            (
                "UX_CycleEligibilityRules_Cycle",
                "An eligibility rule already exists for this cycle. Reload and try again."));
    }

    public async Task<Result<IReadOnlyList<OfferingDto>>> ConfigureOfferingsAsync(Guid cycleId, ConfigureOfferingsRequest request, CancellationToken cancellationToken = default)
    {
        if (cycleId == Guid.Empty)
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(
                400,
                "A valid cycle ID is required.");
        }

        if (request?.Offerings is null)
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(
                400,
                "An offerings list is required.");
        }

        if (request.Offerings.Length == 0)
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(400, "At least one track/branch offering is required.");
        }

        if (request.Offerings.Length > 500)
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(
                400,
                "A cycle can contain at most 500 track/branch offerings.");
        }

        if (request.Offerings.Any(x =>
                x is null ||
                x.TrackId == Guid.Empty ||
                x.BranchId == Guid.Empty ||
                x.Capacity <= 0))
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(400, "Every offering requires a valid track, branch, and positive capacity.");
        }

        if (request.Offerings.GroupBy(x => new { x.TrackId, x.BranchId }).Any(group => group.Count() > 1))
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(400, "Duplicate track/branch combinations are not allowed.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var cycle = await _dbContext.AdmissionCycles.SingleOrDefaultAsync(
                    x => x.Id == cycleId,
                    ct);
                if (cycle is null)
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        404,
                        "Admission cycle was not found.");
                }

                var configurationError = await ValidateCycleIsConfigurableAsync(cycle, ct);
                if (configurationError is not null)
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        409,
                        configurationError);
                }

                var trackIds = request.Offerings
                    .Select(x => x.TrackId)
                    .Distinct()
                    .ToList();
                var branchIds = request.Offerings
                    .Select(x => x.BranchId)
                    .Distinct()
                    .ToList();

                var tracks = await _dbContext.Tracks
                    .Where(x => trackIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

                var branches = await _dbContext.Branches
                    .Where(x => branchIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

                if (tracks.Count != trackIds.Count || branches.Count != branchIds.Count)
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        400,
                        "One or more tracks or branches do not exist.");
                }

                if (tracks.Values.Any(x => x.ProgramId != cycle.ProgramId))
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        400,
                        "Every offered track must belong to the cycle's program.");
                }

                if (tracks.Values.Any(x => !x.IsActive) ||
                    branches.Values.Any(x => !x.IsActive))
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        400,
                        "Inactive tracks or branches cannot be offered.");
                }

                var existing = await _dbContext.TrackBranchOfferings
                    .Where(x => x.CycleId == cycleId)
                    .ToListAsync(ct);
                var now = UtcNow;

                if (existing
                    .GroupBy(offering => new OfferingKey(
                        offering.TrackId,
                        offering.BranchId))
                    .Any(group => group.Count() > 1))
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        409,
                        "Existing offerings contain duplicate track/branch keys. Resolve the database duplicates before updating this cycle.");
                }

                var existingByKey = existing.ToDictionary(
                    offering => new OfferingKey(offering.TrackId, offering.BranchId));

                var requestedKeys = request.Offerings
                    .Select(input => new OfferingKey(input.TrackId, input.BranchId))
                    .ToHashSet();

                var removed = existing
                    .Where(offering =>
                        !requestedKeys.Contains(
                            new OfferingKey(offering.TrackId, offering.BranchId)))
                    .ToList();

                if (await _applicationReader.HasPreferencesForOfferingsAsync(
                        removed.Select(x => x.Id).ToList(),
                        ct))
                {
                    return Result<IReadOnlyList<OfferingDto>>.Failure(
                        409,
                        "One or more removed offerings are referenced by application preferences and cannot be deleted.");
                }

                _dbContext.TrackBranchOfferings.RemoveRange(removed);

                var effectiveOfferings = new List<TrackBranchOffering>(
                    request.Offerings.Length);

                foreach (var input in request.Offerings)
                {
                    var key = new OfferingKey(input.TrackId, input.BranchId);
                    if (existingByKey.TryGetValue(key, out var retained))
                    {
                        if (retained.Capacity != input.Capacity)
                        {
                            retained.Capacity = input.Capacity;
                            retained.UpdatedAt = now;
                        }

                        effectiveOfferings.Add(retained);
                        continue;
                    }

                    var added = new TrackBranchOffering
                    {
                        Id = Guid.NewGuid(),
                        CycleId = cycleId,
                        TrackId = input.TrackId,
                        BranchId = input.BranchId,
                        Capacity = input.Capacity,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    _dbContext.TrackBranchOfferings.Add(added);
                    effectiveOfferings.Add(added);
                }

                cycle.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(ct);

                IReadOnlyList<OfferingDto> data = effectiveOfferings
                    .Select(x => MapOffering(
                        x,
                        tracks[x.TrackId].Name,
                        branches[x.BranchId].Name))
                    .OrderBy(x => x.TrackName)
                    .ThenBy(x => x.BranchName)
                    .ToList();

                return Result<IReadOnlyList<OfferingDto>>.Success(
                    data,
                    message: "Cycle offerings configured successfully.");
            },
            (
                "UX_TrackBranchOfferings_Cycle_Track_Branch",
                "A duplicate track/branch offering already exists for this cycle."));
    }

    public async Task<Result<AdmissionCycleDto>> ActivateCycleAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        if (cycleId == Guid.Empty)
        {
            return Result<AdmissionCycleDto>.Failure(400, "A valid cycle ID is required.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var cycle = await _dbContext.AdmissionCycles
                    .Include(x => x.Program)
                    .Include(x => x.EligibilityRule)
                    .Include(x => x.Offerings)
                        .ThenInclude(x => x.Track)
                    .Include(x => x.Offerings)
                        .ThenInclude(x => x.Branch)
                    .SingleOrDefaultAsync(x => x.Id == cycleId, ct);

                if (cycle is null)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        404,
                        "Admission cycle was not found.");
                }

                if (cycle.Status != CycleStatus.Draft)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        409,
                        "Only a Draft cycle can be activated.");
                }

                if (cycle.DeadlineUtc <= UtcNow)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        400,
                        "The cycle cannot be activated because its deadline has passed.");
                }

                if (cycle.EligibilityRule is null)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        400,
                        "Configure the cycle eligibility rule before activation.");
                }

                if (!await _dbContext.ProgramDocumentRequirements.AnyAsync(
                        x => x.ProgramId == cycle.ProgramId,
                        ct))
                {
                    return Result<AdmissionCycleDto>.Failure(
                        400,
                        "Configure program document requirements before activation.");
                }

                if (cycle.Offerings.Count == 0)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        400,
                        "Configure at least one track/branch offering before activation.");
                }

                if (cycle.Offerings.Any(x =>
                        x.Capacity <= 0 ||
                        !x.Track.IsActive ||
                        !x.Branch.IsActive ||
                        x.Track.ProgramId != cycle.ProgramId))
                {
                    return Result<AdmissionCycleDto>.Failure(
                        400,
                        "All offerings must use active tracks and branches, positive capacity, and tracks belonging to the cycle's program.");
                }

                if (await _dbContext.AdmissionCycles.AnyAsync(
                        x => x.Id != cycleId && x.Status == CycleStatus.Active,
                        ct))
                {
                    return Result<AdmissionCycleDto>.Failure(
                        409,
                        "Another admission cycle is already active.");
                }

                cycle.Status = CycleStatus.Active;
                cycle.ClosedAt = null;
                cycle.UpdatedAt = UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                return Result<AdmissionCycleDto>.Success(
                    MapCycle(cycle),
                    message: "Admission cycle activated successfully.");
            },
            (
                "uq_cycle_active",
                "The cycle could not be activated because another cycle became active at the same time."));
    }

    public async Task<Result<AdmissionCycleDto>> CloseCycleAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        if (cycleId == Guid.Empty)
        {
            return Result<AdmissionCycleDto>.Failure(400, "A valid cycle ID is required.");
        }

        return await ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var cycle = await _dbContext.AdmissionCycles
                    .Include(x => x.Program)
                    .Include(x => x.EligibilityRule)
                    .Include(x => x.Offerings)
                        .ThenInclude(x => x.Track)
                    .Include(x => x.Offerings)
                        .ThenInclude(x => x.Branch)
                    .SingleOrDefaultAsync(x => x.Id == cycleId, ct);

                if (cycle is null)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        404,
                        "Admission cycle was not found.");
                }

                if (cycle.Status != CycleStatus.Active)
                {
                    return Result<AdmissionCycleDto>.Failure(
                        409,
                        "Only an Active cycle can be closed.");
                }

                var now = UtcNow;
                cycle.Status = CycleStatus.Closed;
                cycle.ClosedAt = now;
                cycle.UpdatedAt = now;

                await _dbContext.SaveChangesAsync(ct);

                return Result<AdmissionCycleDto>.Success(
                    MapCycle(cycle),
                    message: "Admission cycle closed successfully.");
            });
    }

    public async Task<AdminAdmissionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var activeCycle = await _dbContext.AdmissionCycles
            .AsNoTracking()
            .Include(x => x.Program)
            .Include(x => x.EligibilityRule)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Track)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Status == CycleStatus.Active, cancellationToken);

        return new AdminAdmissionDashboardDto
        {
            InstitutionCount = await _dbContext.Institutions.CountAsync(cancellationToken),
            ProgramCount = await _dbContext.Programs.CountAsync(cancellationToken),
            ActiveTrackCount = await _dbContext.Tracks.CountAsync(x => x.IsActive, cancellationToken),
            ActiveBranchCount = await _dbContext.Branches.CountAsync(x => x.IsActive, cancellationToken),
            DraftCycleCount = await _dbContext.AdmissionCycles.CountAsync(x => x.Status == CycleStatus.Draft, cancellationToken),
            ClosedCycleCount = await _dbContext.AdmissionCycles.CountAsync(x => x.Status == CycleStatus.Closed, cancellationToken),
            ApplicationCount = await _applicationReader.CountApplicationsAsync(cancellationToken),
            ActiveCycle = activeCycle is null ? null : MapCycle(activeCycle),
            ActiveCycleOfferingCount = activeCycle?.Offerings.Count ?? 0,
            ActiveCycleCapacity = activeCycle?.Offerings.Sum(x => x.Capacity) ?? 0
        };
    }

    private async Task<string?> ValidateCycleIsConfigurableAsync(AdmissionCycle cycle, CancellationToken cancellationToken)
    {
        if (cycle.Status != CycleStatus.Draft)
        {
            return "Only Draft cycles can be configured.";
        }

        if (await _applicationReader.HasApplicationsForCycleAsync(
                cycle.Id,
                cancellationToken))
        {
            return "Cycle configuration is locked because applications already exist for this cycle.";
        }

        return null;
    }

    private async Task<Result<T>> ExecuteWriteAsync<T>(
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<Result<T>>> operation,
        params (string ConstraintName, string Message)[] uniqueConflicts)
    {
        try
        {
            return await operation(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<T>.Failure(
                409,
                "The configuration was changed by another administrator. Reload it and try again.");
        }
        catch (DbUpdateException exception)
        {
            var mapped = MapDatabaseException<T>(exception, uniqueConflicts);
            if (mapped is not null)
            {
                return mapped;
            }

            throw;
        }
        catch (PostgresException exception)
        {
            var mapped = MapDatabaseException<T>(exception, uniqueConflicts);
            if (mapped is not null)
            {
                return mapped;
            }

            throw;
        }
    }

    private Task<Result<T>> ExecuteSerializableAsync<T>(
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<Result<T>>> operation,
        params (string ConstraintName, string Message)[] uniqueConflicts)
    {
        return ExecuteWriteAsync(
            cancellationToken,
            async ct =>
            {
                await using var transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var result = await operation(ct);
                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(ct);
                }

                return result;
            },
            uniqueConflicts);
    }

    private static Result<T>? MapDatabaseException<T>(
        Exception exception,
        IReadOnlyCollection<(string ConstraintName, string Message)> uniqueConflicts)
    {
        var postgresException = FindPostgresException(exception);
        if (postgresException is null)
        {
            return null;
        }

        if (postgresException.SqlState is
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected)
        {
            return Result<T>.Failure(
                409,
                "The configuration changed concurrently. Reload it and try again.");
        }

        if (postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            var conflict = uniqueConflicts.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ConstraintName,
                    postgresException.ConstraintName,
                    StringComparison.Ordinal));

            return string.IsNullOrEmpty(conflict.ConstraintName)
                ? null
                : Result<T>.Failure(409, conflict.Message);
        }

        if (postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return Result<T>.Failure(
                409,
                "A referenced configuration item changed or no longer exists. Reload and try again.");
        }

        if (postgresException.SqlState == PostgresErrorCodes.CheckViolation)
        {
            return Result<T>.Failure(
                400,
                "The submitted configuration violates a database validation rule.");
        }

        return null;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    private static string NormalizeRequiredText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeCode(string? value)
    {
        return NormalizeRequiredText(value).ToUpperInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsRequiredTextValid(string? value, int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;
    }

    internal static TopicValidationResult ValidateAndNormalizeTopics(
        IEnumerable<string>? topics)
    {
        var suppliedTopics = topics?.ToList() ?? new List<string>();
        if (suppliedTopics.Count > 50)
        {
            return TopicValidationResult.Invalid(
                "A track can contain at most 50 prerequisite topics.");
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string? suppliedTopic in suppliedTopics)
        {
            if (string.IsNullOrWhiteSpace(suppliedTopic))
            {
                continue;
            }

            string topic = suppliedTopic.Trim();
            if (topic.Length > 100)
            {
                return TopicValidationResult.Invalid(
                    "Each prerequisite topic must not exceed 100 characters.");
            }

            if (seen.Add(topic))
            {
                normalized.Add(topic);
            }
        }

        return TopicValidationResult.Valid(normalized);
    }

    private readonly record struct RequirementKey(
        DocumentType DocumentType,
        Gender? RequiredForGender);

    private readonly record struct OfferingKey(Guid TrackId, Guid BranchId);

    internal readonly record struct TopicValidationResult(
        bool IsValid,
        List<string> Topics,
        string ErrorMessage)
    {
        public static TopicValidationResult Valid(List<string> topics)
        {
            return new TopicValidationResult(true, topics, string.Empty);
        }

        public static TopicValidationResult Invalid(string errorMessage)
        {
            return new TopicValidationResult(false, new List<string>(), errorMessage);
        }
    }

    private static InstitutionDto MapInstitution(Institution institution, int programCount)
    {
        return new InstitutionDto
        {
            Id = institution.Id,
            Name = institution.Name,
            Code = institution.Code,
            ProgramCount = programCount
        };
    }

    private static ProgramDto MapProgram(Program program, string institutionName, int trackCount, int cycleCount)
    {
        return new ProgramDto
        {
            Id = program.Id,
            InstitutionId = program.InstitutionId,
            InstitutionName = institutionName,
            Name = program.Name,
            Code = program.Code,
            DurationMonths = program.DurationMonths,
            TrackCount = trackCount,
            CycleCount = cycleCount
        };
    }

    private static ProgramDocumentRequirementDto MapRequirement(ProgramDocumentRequirement requirement)
    {
        return new ProgramDocumentRequirementDto
        {
            Id = requirement.Id,
            ProgramId = requirement.ProgramId,
            DocumentType = requirement.DocumentType,
            RequiredForGender = requirement.RequiredForGender
        };
    }

    private static TrackDto MapTrack(Track track, IReadOnlyList<BranchOfferingDto> offerings)
    {
        return new TrackDto
        {
            Id = track.Id,
            ProgramId = track.ProgramId,
            Name = track.Name,
            Description = track.Description,
            PrerequisiteTopics = track.PrerequisiteTopics.ToList(),
            IsActive = track.IsActive,
            Offerings = offerings
        };
    }

    private static BranchDto MapBranch(Branch branch)
    {
        return new BranchDto
        {
            Id = branch.Id,
            Name = branch.Name,
            Governorate = branch.Governorate,
            IsActive = branch.IsActive
        };
    }

    private static BranchOfferingDto MapBranchOffering(TrackBranchOffering offering)
    {
        return new BranchOfferingDto
        {
            OfferingId = offering.Id,
            BranchId = offering.BranchId,
            BranchName = offering.Branch.Name,
            Governorate = offering.Branch.Governorate,
            Capacity = offering.Capacity
        };
    }

    private static CycleEligibilityRuleDto MapEligibilityRule(CycleEligibilityRule rule)
    {
        return new CycleEligibilityRuleDto
        {
            Id = rule.Id,
            CycleId = rule.CycleId,
            RequiredNationality = rule.RequiredNationality,
            RequiredDegreeLevel = rule.RequiredDegreeLevel,
            MaxYearsSinceGraduation = rule.MaxYearsSinceGraduation,
            MinGrade = rule.MinGrade
        };
    }

    private static OfferingDto MapOffering(TrackBranchOffering offering, string trackName, string branchName)
    {
        return new OfferingDto
        {
            Id = offering.Id,
            CycleId = offering.CycleId,
            TrackId = offering.TrackId,
            TrackName = trackName,
            BranchId = offering.BranchId,
            BranchName = branchName,
            Capacity = offering.Capacity
        };
    }

    private static AdmissionCycleDto MapCycle(
        AdmissionCycle cycle,
        string? programName = null)
    {
        return new AdmissionCycleDto
        {
            Id = cycle.Id,
            ProgramId = cycle.ProgramId,
            ProgramName = programName ?? cycle.Program?.Name ?? string.Empty,
            Label = cycle.Label,
            StartDate = cycle.StartDate,
            DeadlineUtc = cycle.DeadlineUtc,
            Status = cycle.Status,
            ClosedAt = cycle.ClosedAt,
            RowVersion = cycle.RowVersion,
            EligibilityRule = cycle.EligibilityRule is null ? null : MapEligibilityRule(cycle.EligibilityRule),
            Offerings = cycle.Offerings
                .Select(x => MapOffering(x, x.Track?.Name ?? string.Empty, x.Branch?.Name ?? string.Empty))
                .OrderBy(x => x.TrackName)
                .ThenBy(x => x.BranchName)
                .ToList()
        };
    }
}
