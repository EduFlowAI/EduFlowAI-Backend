using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EduFlowAI.Admission.Application.Services.Configuration;

public sealed class AdmissionOperationResult<T>
{
    public bool Succeeded { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static AdmissionOperationResult<T> Success(T data, string message = "Success", int statusCode = 200)
    {
        return new AdmissionOperationResult<T>
        {
            Succeeded = true,
            StatusCode = statusCode,
            Message = message,
            Data = data
        };
    }

    public static AdmissionOperationResult<T> Failure(int statusCode, string message)
    {
        return new AdmissionOperationResult<T>
        {
            Succeeded = false,
            StatusCode = statusCode,
            Message = message,
            Data = default
        };
    }
}

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

    [Required, MaxLength(30)]
    public string Code { get; init; } = string.Empty;
}

public sealed class UpdateInstitutionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(30)]
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
    [Required]
    public List<ProgramDocumentRequirementInput> Requirements { get; init; } = new();
}

public sealed class CreateTrackRequest
{
    public Guid ProgramId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
    public List<string> PrerequisiteTopics { get; init; } = new();
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateTrackRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
    public List<string> PrerequisiteTopics { get; init; } = new();
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
    [Required]
    public List<OfferingInput> Offerings { get; init; } = new();
}

public interface IAdmissionConfigurationService
{
    Task<IReadOnlyList<TrackDto>> GetPublicTracksAsync(CancellationToken cancellationToken = default);
    Task<TrackDto?> GetPublicTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<InstitutionDto>> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<InstitutionDto>> UpdateInstitutionAsync(Guid institutionId, UpdateInstitutionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramDto>> GetProgramsAsync(CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<ProgramDto>> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<ProgramDto>> UpdateProgramAsync(Guid programId, UpdateProgramRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProgramDocumentRequirementDto>?> GetProgramRequirementsAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>> ReplaceProgramRequirementsAsync(Guid programId, UpdateProgramDocumentRequirementsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<TrackDto>> CreateTrackAsync(CreateTrackRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<TrackDto>> UpdateTrackAsync(Guid trackId, UpdateTrackRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<BranchDto>> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<BranchDto>> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdmissionCycleDto>> GetCyclesAsync(CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<AdmissionCycleDto>> CreateCycleAsync(CreateAdmissionCycleRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<CycleEligibilityRuleDto>> UpsertEligibilityRuleAsync(Guid cycleId, UpdateCycleEligibilityRuleRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<IReadOnlyList<OfferingDto>>> ConfigureOfferingsAsync(Guid cycleId, ConfigureOfferingsRequest request, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<AdmissionCycleDto>> ActivateCycleAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<AdmissionOperationResult<AdmissionCycleDto>> CloseCycleAsync(Guid cycleId, CancellationToken cancellationToken = default);

    Task<AdminAdmissionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public sealed class AdmissionConfigurationService : IAdmissionConfigurationService
{
    private readonly IAdmissionDbContext _dbContext;

    public AdmissionConfigurationService(IAdmissionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TrackDto>> GetPublicTracksAsync(CancellationToken cancellationToken = default)
    {
        var offerings = await _dbContext.TrackBranchOfferings
            .AsNoTracking()
            .Include(x => x.Cycle)
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
            .Include(x => x.Cycle)
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

    public async Task<AdmissionOperationResult<InstitutionDto>> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default)
    {
        string code = NormalizeCode(request.Code);
        string name = request.Name.Trim();

        if (await _dbContext.Institutions.AnyAsync(x => x.Code == code, cancellationToken))
        {
            return AdmissionOperationResult<InstitutionDto>.Failure(409, $"Institution code '{code}' already exists.");
        }

        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code
        };

        _dbContext.Institutions.Add(institution);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdmissionOperationResult<InstitutionDto>.Success(
            MapInstitution(institution, 0),
            "Institution created successfully.",
            201);
    }

    public async Task<AdmissionOperationResult<InstitutionDto>> UpdateInstitutionAsync(Guid institutionId, UpdateInstitutionRequest request, CancellationToken cancellationToken = default)
    {
        var institution = await _dbContext.Institutions
            .SingleOrDefaultAsync(x => x.Id == institutionId, cancellationToken);

        if (institution is null)
        {
            return AdmissionOperationResult<InstitutionDto>.Failure(404, "Institution was not found.");
        }

        if (await InstitutionHasApplicationsAsync(institutionId, cancellationToken))
        {
            return AdmissionOperationResult<InstitutionDto>.Failure(409, "Institution configuration is locked because admissions have already started for one of its programs.");
        }

        string code = NormalizeCode(request.Code);
        if (await _dbContext.Institutions.AnyAsync(x => x.Id != institutionId && x.Code == code, cancellationToken))
        {
            return AdmissionOperationResult<InstitutionDto>.Failure(409, $"Institution code '{code}' already exists.");
        }

        institution.Name = request.Name.Trim();
        institution.Code = code;
        await _dbContext.SaveChangesAsync(cancellationToken);

        int programCount = await _dbContext.Programs.CountAsync(x => x.InstitutionId == institutionId, cancellationToken);
        return AdmissionOperationResult<InstitutionDto>.Success(MapInstitution(institution, programCount), "Institution updated successfully.");
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

    public async Task<AdmissionOperationResult<ProgramDto>> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default)
    {
        var institution = await _dbContext.Institutions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.InstitutionId, cancellationToken);

        if (institution is null)
        {
            return AdmissionOperationResult<ProgramDto>.Failure(404, "Institution was not found.");
        }

        string code = NormalizeCode(request.Code);
        if (await _dbContext.Programs.AnyAsync(x => x.Code == code, cancellationToken))
        {
            return AdmissionOperationResult<ProgramDto>.Failure(409, $"Program code '{code}' already exists.");
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            InstitutionId = request.InstitutionId,
            Name = request.Name.Trim(),
            Code = code,
            DurationMonths = request.DurationMonths
        };

        _dbContext.Programs.Add(program);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdmissionOperationResult<ProgramDto>.Success(
            MapProgram(program, institution.Name, 0, 0),
            "Program created successfully.",
            201);
    }

    public async Task<AdmissionOperationResult<ProgramDto>> UpdateProgramAsync(Guid programId, UpdateProgramRequest request, CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.Programs
            .Include(x => x.Institution)
            .SingleOrDefaultAsync(x => x.Id == programId, cancellationToken);

        if (program is null)
        {
            return AdmissionOperationResult<ProgramDto>.Failure(404, "Program was not found.");
        }

        if (await ProgramHasApplicationsAsync(programId, cancellationToken))
        {
            return AdmissionOperationResult<ProgramDto>.Failure(409, "Program configuration is locked because admissions have already started.");
        }

        string code = NormalizeCode(request.Code);
        if (await _dbContext.Programs.AnyAsync(x => x.Id != programId && x.Code == code, cancellationToken))
        {
            return AdmissionOperationResult<ProgramDto>.Failure(409, $"Program code '{code}' already exists.");
        }

        program.Name = request.Name.Trim();
        program.Code = code;
        program.DurationMonths = request.DurationMonths;
        await _dbContext.SaveChangesAsync(cancellationToken);

        int trackCount = await _dbContext.Tracks.CountAsync(x => x.ProgramId == programId, cancellationToken);
        int cycleCount = await _dbContext.AdmissionCycles.CountAsync(x => x.ProgramId == programId, cancellationToken);

        return AdmissionOperationResult<ProgramDto>.Success(
            MapProgram(program, program.Institution.Name, trackCount, cycleCount),
            "Program updated successfully.");
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
            .Select(x => MapRequirement(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>> ReplaceProgramRequirementsAsync(Guid programId, UpdateProgramDocumentRequirementsRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.Programs.AnyAsync(x => x.Id == programId, cancellationToken))
        {
            return AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(404, "Program was not found.");
        }

        if (await ProgramHasApplicationsAsync(programId, cancellationToken))
        {
            return AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(409, "Document requirements are locked because admissions have already started for this program.");
        }

        if (request.Requirements.Count == 0)
        {
            return AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(400, "At least one document requirement is required.");
        }

        if (request.Requirements.Any(x => x.DocumentType == DocumentType.None || x.RequiredForGender == Gender.None))
        {
            return AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(400, "DocumentType.None and Gender.None are not valid requirement values. Use null for all genders.");
        }

        bool hasDuplicates = request.Requirements
            .GroupBy(x => new { x.DocumentType, x.RequiredForGender })
            .Any(group => group.Count() > 1);

        if (hasDuplicates)
        {
            return AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>.Failure(400, "Duplicate document requirement combinations are not allowed.");
        }

        var existing = await _dbContext.ProgramDocumentRequirements
            .Where(x => x.ProgramId == programId)
            .ToListAsync(cancellationToken);

        _dbContext.ProgramDocumentRequirements.RemoveRange(existing);

        var replacements = request.Requirements.Select(input => new ProgramDocumentRequirement
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            DocumentType = input.DocumentType,
            RequiredForGender = input.RequiredForGender
        }).ToList();

        _dbContext.ProgramDocumentRequirements.AddRange(replacements);
        await _dbContext.SaveChangesAsync(cancellationToken);

        IReadOnlyList<ProgramDocumentRequirementDto> data = replacements
            .OrderBy(x => x.DocumentType)
            .ThenBy(x => x.RequiredForGender)
            .Select(MapRequirement)
            .ToList();

        return AdmissionOperationResult<IReadOnlyList<ProgramDocumentRequirementDto>>.Success(data, "Program document requirements updated successfully.");
    }

    public async Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(CancellationToken cancellationToken = default)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return tracks.Select(x => MapTrack(x, Array.Empty<BranchOfferingDto>())).ToList();
    }

    public async Task<AdmissionOperationResult<TrackDto>> CreateTrackAsync(CreateTrackRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.Programs.AnyAsync(x => x.Id == request.ProgramId, cancellationToken))
        {
            return AdmissionOperationResult<TrackDto>.Failure(404, "Program was not found.");
        }

        string name = request.Name.Trim();
        if (await _dbContext.Tracks.AnyAsync(x => x.ProgramId == request.ProgramId && x.Name == name, cancellationToken))
        {
            return AdmissionOperationResult<TrackDto>.Failure(409, "A track with the same name already exists in this program.");
        }

        var track = new Track
        {
            Id = Guid.NewGuid(),
            ProgramId = request.ProgramId,
            Name = name,
            Description = NormalizeOptionalText(request.Description),
            PrerequisiteTopics = NormalizeTopics(request.PrerequisiteTopics),
            IsActive = request.IsActive
        };

        _dbContext.Tracks.Add(track);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdmissionOperationResult<TrackDto>.Success(MapTrack(track, Array.Empty<BranchOfferingDto>()), "Track created successfully.", 201);
    }

    public async Task<AdmissionOperationResult<TrackDto>> UpdateTrackAsync(Guid trackId, UpdateTrackRequest request, CancellationToken cancellationToken = default)
    {
        var track = await _dbContext.Tracks.SingleOrDefaultAsync(x => x.Id == trackId, cancellationToken);
        if (track is null)
        {
            return AdmissionOperationResult<TrackDto>.Failure(404, "Track was not found.");
        }

        if (await TrackHasApplicationsAsync(trackId, cancellationToken))
        {
            return AdmissionOperationResult<TrackDto>.Failure(409, "Track configuration is locked because an application already references one of its offerings.");
        }

        string name = request.Name.Trim();
        if (await _dbContext.Tracks.AnyAsync(x => x.Id != trackId && x.ProgramId == track.ProgramId && x.Name == name, cancellationToken))
        {
            return AdmissionOperationResult<TrackDto>.Failure(409, "A track with the same name already exists in this program.");
        }

        track.Name = name;
        track.Description = NormalizeOptionalText(request.Description);
        track.PrerequisiteTopics = NormalizeTopics(request.PrerequisiteTopics);
        track.IsActive = request.IsActive;
        track.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AdmissionOperationResult<TrackDto>.Success(MapTrack(track, Array.Empty<BranchOfferingDto>()), "Track updated successfully.");
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Branches
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => MapBranch(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdmissionOperationResult<BranchDto>> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        string name = request.Name.Trim();
        if (await _dbContext.Branches.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return AdmissionOperationResult<BranchDto>.Failure(409, "A branch with the same name already exists.");
        }

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = name,
            Governorate = NormalizeOptionalText(request.Governorate),
            IsActive = request.IsActive
        };

        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdmissionOperationResult<BranchDto>.Success(MapBranch(branch), "Branch created successfully.", 201);
    }

    public async Task<AdmissionOperationResult<BranchDto>> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _dbContext.Branches.SingleOrDefaultAsync(x => x.Id == branchId, cancellationToken);
        if (branch is null)
        {
            return AdmissionOperationResult<BranchDto>.Failure(404, "Branch was not found.");
        }

        if (await BranchHasApplicationsAsync(branchId, cancellationToken))
        {
            return AdmissionOperationResult<BranchDto>.Failure(409, "Branch configuration is locked because an application already references one of its offerings.");
        }

        string name = request.Name.Trim();
        if (await _dbContext.Branches.AnyAsync(x => x.Id != branchId && x.Name == name, cancellationToken))
        {
            return AdmissionOperationResult<BranchDto>.Failure(409, "A branch with the same name already exists.");
        }

        branch.Name = name;
        branch.Governorate = NormalizeOptionalText(request.Governorate);
        branch.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdmissionOperationResult<BranchDto>.Success(MapBranch(branch), "Branch updated successfully.");
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

    public async Task<AdmissionOperationResult<AdmissionCycleDto>> CreateCycleAsync(CreateAdmissionCycleRequest request, CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.Programs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.ProgramId, cancellationToken);

        if (program is null)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(404, "Program was not found.");
        }

        if (request.StartDate == default)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "StartDate is required.");
        }

        if (request.DeadlineUtc <= DateTimeOffset.UtcNow)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "The application deadline must be in the future.");
        }

        if (request.StartDate > DateOnly.FromDateTime(request.DeadlineUtc.UtcDateTime))
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "StartDate cannot be after the application deadline.");
        }

        string label = request.Label.Trim();
        if (await _dbContext.AdmissionCycles.AnyAsync(x => x.ProgramId == request.ProgramId && x.Label == label, cancellationToken))
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "A cycle with the same label already exists for this program.");
        }

        var cycle = new AdmissionCycle
        {
            Id = Guid.NewGuid(),
            ProgramId = request.ProgramId,
            Label = label,
            StartDate = request.StartDate,
            DeadlineUtc = request.DeadlineUtc.ToUniversalTime(),
            Status = CycleStatus.Draft
        };

        _dbContext.AdmissionCycles.Add(cycle);
        await _dbContext.SaveChangesAsync(cancellationToken);

        cycle.Program = program;
        return AdmissionOperationResult<AdmissionCycleDto>.Success(MapCycle(cycle), "Admission cycle created as Draft.", 201);
    }

    public async Task<AdmissionOperationResult<CycleEligibilityRuleDto>> UpsertEligibilityRuleAsync(Guid cycleId, UpdateCycleEligibilityRuleRequest request, CancellationToken cancellationToken = default)
    {
        var cycle = await _dbContext.AdmissionCycles.SingleOrDefaultAsync(x => x.Id == cycleId, cancellationToken);
        if (cycle is null)
        {
            return AdmissionOperationResult<CycleEligibilityRuleDto>.Failure(404, "Admission cycle was not found.");
        }

        var configurationError = await ValidateCycleIsConfigurableAsync(cycle, cancellationToken);
        if (configurationError is not null)
        {
            return AdmissionOperationResult<CycleEligibilityRuleDto>.Failure(409, configurationError);
        }

        if (request.MinGrade == CumulativeGrade.None)
        {
            return AdmissionOperationResult<CycleEligibilityRuleDto>.Failure(400, "MinGrade.None is not valid.");
        }

        string nationality = request.RequiredNationality.Trim().ToUpperInvariant();
        string degreeLevel = request.RequiredDegreeLevel.Trim();

        var rule = await _dbContext.CycleEligibilityRules.SingleOrDefaultAsync(x => x.CycleId == cycleId, cancellationToken);
        if (rule is null)
        {
            rule = new CycleEligibilityRule
            {
                Id = Guid.NewGuid(),
                CycleId = cycleId
            };
            _dbContext.CycleEligibilityRules.Add(rule);
        }

        rule.RequiredNationality = nationality;
        rule.RequiredDegreeLevel = degreeLevel;
        rule.MaxYearsSinceGraduation = request.MaxYearsSinceGraduation;
        rule.MinGrade = request.MinGrade;
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AdmissionOperationResult<CycleEligibilityRuleDto>.Success(MapEligibilityRule(rule), "Cycle eligibility rule saved successfully.");
    }

    public async Task<AdmissionOperationResult<IReadOnlyList<OfferingDto>>> ConfigureOfferingsAsync(Guid cycleId, ConfigureOfferingsRequest request, CancellationToken cancellationToken = default)
    {
        var cycle = await _dbContext.AdmissionCycles.SingleOrDefaultAsync(x => x.Id == cycleId, cancellationToken);
        if (cycle is null)
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(404, "Admission cycle was not found.");
        }

        var configurationError = await ValidateCycleIsConfigurableAsync(cycle, cancellationToken);
        if (configurationError is not null)
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(409, configurationError);
        }

        if (request.Offerings.Count == 0)
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(400, "At least one track/branch offering is required.");
        }

        if (request.Offerings.Any(x => x.TrackId == Guid.Empty || x.BranchId == Guid.Empty || x.Capacity <= 0))
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(400, "Every offering requires a valid track, branch, and positive capacity.");
        }

        if (request.Offerings.GroupBy(x => new { x.TrackId, x.BranchId }).Any(group => group.Count() > 1))
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(400, "Duplicate track/branch combinations are not allowed.");
        }

        var trackIds = request.Offerings.Select(x => x.TrackId).Distinct().ToList();
        var branchIds = request.Offerings.Select(x => x.BranchId).Distinct().ToList();

        var tracks = await _dbContext.Tracks
            .Where(x => trackIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var branches = await _dbContext.Branches
            .Where(x => branchIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (tracks.Count != trackIds.Count || branches.Count != branchIds.Count)
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(400, "One or more tracks or branches do not exist.");
        }

        if (tracks.Values.Any(x => x.ProgramId != cycle.ProgramId))
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(400, "Every offered track must belong to the cycle's program.");
        }

        if (tracks.Values.Any(x => !x.IsActive) || branches.Values.Any(x => !x.IsActive))
        {
            return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Failure(400, "Inactive tracks or branches cannot be offered.");
        }

        var existing = await _dbContext.TrackBranchOfferings
            .Where(x => x.CycleId == cycleId)
            .ToListAsync(cancellationToken);

        _dbContext.TrackBranchOfferings.RemoveRange(existing);

        var replacements = request.Offerings.Select(input => new TrackBranchOffering
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            TrackId = input.TrackId,
            BranchId = input.BranchId,
            Capacity = input.Capacity
        }).ToList();

        _dbContext.TrackBranchOfferings.AddRange(replacements);
        await _dbContext.SaveChangesAsync(cancellationToken);

        IReadOnlyList<OfferingDto> data = replacements
            .Select(x => MapOffering(x, tracks[x.TrackId].Name, branches[x.BranchId].Name))
            .OrderBy(x => x.TrackName)
            .ThenBy(x => x.BranchName)
            .ToList();

        return AdmissionOperationResult<IReadOnlyList<OfferingDto>>.Success(data, "Cycle offerings configured successfully.");
    }

    public async Task<AdmissionOperationResult<AdmissionCycleDto>> ActivateCycleAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var cycle = await _dbContext.AdmissionCycles
            .Include(x => x.Program)
            .Include(x => x.EligibilityRule)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Track)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == cycleId, cancellationToken);

        if (cycle is null)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(404, "Admission cycle was not found.");
        }

        if (cycle.Status != CycleStatus.Draft)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "Only a Draft cycle can be activated.");
        }

        if (cycle.DeadlineUtc <= DateTimeOffset.UtcNow)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "The cycle cannot be activated because its deadline has passed.");
        }

        if (cycle.EligibilityRule is null)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "Configure the cycle eligibility rule before activation.");
        }

        if (!await _dbContext.ProgramDocumentRequirements.AnyAsync(x => x.ProgramId == cycle.ProgramId, cancellationToken))
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "Configure program document requirements before activation.");
        }

        if (cycle.Offerings.Count == 0)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "Configure at least one track/branch offering before activation.");
        }

        if (cycle.Offerings.Any(x =>
            x.Capacity <= 0 ||
            !x.Track.IsActive ||
            !x.Branch.IsActive ||
            x.Track.ProgramId != cycle.ProgramId))
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(400, "All offerings must use active tracks and branches, positive capacity, and tracks belonging to the cycle's program.");
        }

        if (await _dbContext.AdmissionCycles.AnyAsync(x => x.Id != cycleId && x.Status == CycleStatus.Active, cancellationToken))
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "Another admission cycle is already active.");
        }

        cycle.Status = CycleStatus.Active;
        cycle.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "The cycle was changed by another administrator. Reload it and try again.");
        }
        catch (DbUpdateException)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "The cycle could not be activated because another cycle became active at the same time.");
        }

        return AdmissionOperationResult<AdmissionCycleDto>.Success(MapCycle(cycle), "Admission cycle activated successfully.");
    }

    public async Task<AdmissionOperationResult<AdmissionCycleDto>> CloseCycleAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var cycle = await _dbContext.AdmissionCycles
            .Include(x => x.Program)
            .Include(x => x.EligibilityRule)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Track)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == cycleId, cancellationToken);

        if (cycle is null)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(404, "Admission cycle was not found.");
        }

        if (cycle.Status != CycleStatus.Active)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "Only an Active cycle can be closed.");
        }

        cycle.Status = CycleStatus.Closed;
        cycle.ClosedAt = DateTimeOffset.UtcNow;
        cycle.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdmissionOperationResult<AdmissionCycleDto>.Failure(409, "The cycle was changed by another administrator. Reload it and try again.");
        }

        return AdmissionOperationResult<AdmissionCycleDto>.Success(MapCycle(cycle), "Admission cycle closed successfully.");
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
            ApplicationCount = await _dbContext.Applications.CountAsync(cancellationToken),
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

        if (await _dbContext.Applications.AnyAsync(x => x.CycleId == cycle.Id, cancellationToken))
        {
            return "Cycle configuration is locked because applications already exist for this cycle.";
        }

        return null;
    }

    private async Task<bool> InstitutionHasApplicationsAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var programIds = _dbContext.Programs
            .Where(x => x.InstitutionId == institutionId)
            .Select(x => x.Id);

        var cycleIds = _dbContext.AdmissionCycles
            .Where(x => programIds.Contains(x.ProgramId))
            .Select(x => x.Id);

        return await _dbContext.Applications.AnyAsync(x => cycleIds.Contains(x.CycleId), cancellationToken);
    }

    private async Task<bool> ProgramHasApplicationsAsync(Guid programId, CancellationToken cancellationToken)
    {
        var cycleIds = _dbContext.AdmissionCycles
            .Where(x => x.ProgramId == programId)
            .Select(x => x.Id);

        return await _dbContext.Applications.AnyAsync(x => cycleIds.Contains(x.CycleId), cancellationToken);
    }

    private async Task<bool> TrackHasApplicationsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var cycleIds = _dbContext.TrackBranchOfferings
            .Where(x => x.TrackId == trackId)
            .Select(x => x.CycleId);

        return await _dbContext.Applications.AnyAsync(x => cycleIds.Contains(x.CycleId), cancellationToken);
    }

    private async Task<bool> BranchHasApplicationsAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var cycleIds = _dbContext.TrackBranchOfferings
            .Where(x => x.BranchId == branchId)
            .Select(x => x.CycleId);

        return await _dbContext.Applications.AnyAsync(x => cycleIds.Contains(x.CycleId), cancellationToken);
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> NormalizeTopics(IEnumerable<string>? topics)
    {
        return topics?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
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

    private static AdmissionCycleDto MapCycle(AdmissionCycle cycle)
    {
        return new AdmissionCycleDto
        {
            Id = cycle.Id,
            ProgramId = cycle.ProgramId,
            ProgramName = cycle.Program?.Name ?? string.Empty,
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
