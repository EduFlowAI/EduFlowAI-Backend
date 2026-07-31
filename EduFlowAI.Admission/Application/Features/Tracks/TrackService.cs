using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Features.Configuration.Common;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EduFlowAI.Admission.Application.Features.Tracks;

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

public interface ITrackService
{
    Task<IReadOnlyList<TrackDto>> GetPublicTracksAsync(
        CancellationToken cancellationToken = default);

    Task<TrackDto?> GetPublicTrackAsync(
        Guid trackId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(
        CancellationToken cancellationToken = default);

    Task<Result<TrackDto>> CreateTrackAsync(
        CreateTrackRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TrackDto>> UpdateTrackAsync(
        Guid trackId,
        UpdateTrackRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class TrackService : ITrackService
{
    private readonly IAdmissionDbContext _dbContext;
    private readonly IAdmissionApplicationReader _applicationReader;
    private readonly TimeProvider _timeProvider;
    private readonly AdmissionWriteExecutor _writeExecutor;

    public TrackService(
        IAdmissionDbContext dbContext,
        IAdmissionApplicationReader applicationReader,
        TimeProvider timeProvider,
        AdmissionWriteExecutor writeExecutor)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(applicationReader);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(writeExecutor);

        _dbContext = dbContext;
        _applicationReader = applicationReader;
        _timeProvider = timeProvider;
        _writeExecutor = writeExecutor;
    }

    public async Task<IReadOnlyList<TrackDto>> GetPublicTracksAsync(
        CancellationToken cancellationToken = default)
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
            .Select(group => MapTrack(
                group.First().Track,
                group.Select(MapBranchOffering).ToList()))
            .ToList();
    }

    public async Task<TrackDto?> GetPublicTrackAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
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

        return MapTrack(
            offerings[0].Track,
            offerings.Select(MapBranchOffering).ToList());
    }

    public async Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(
        CancellationToken cancellationToken = default)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return tracks
            .Select(x => MapTrack(x, Array.Empty<BranchOfferingDto>()))
            .ToList();
    }

    public async Task<Result<TrackDto>> CreateTrackAsync(
        CreateTrackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<TrackDto>.Failure(400, "Request body is required.");
        }

        if (request.ProgramId == Guid.Empty)
        {
            return Result<TrackDto>.Failure(
                400,
                "A valid program ID is required.");
        }

        string name = AdmissionConfigurationText.NormalizeRequired(request.Name);
        string? description = AdmissionConfigurationText.NormalizeOptional(
            request.Description);

        if (!AdmissionConfigurationText.IsRequiredValid(name, 200) ||
            description?.Length > 4000)
        {
            return Result<TrackDto>.Failure(
                400,
                "Track name is required and must not exceed 200 characters; description must not exceed 4000 characters.");
        }

        var topicValidation = TrackTopicValidator.Validate(
            request.PrerequisiteTopics);

        if (!topicValidation.IsValid)
        {
            return Result<TrackDto>.Failure(
                400,
                topicValidation.ErrorMessage);
        }

        return await _writeExecutor.ExecuteAsync(
            cancellationToken,
            async ct =>
            {
                if (!await _dbContext.Programs.AnyAsync(
                        x => x.Id == request.ProgramId,
                        ct))
                {
                    return Result<TrackDto>.Failure(
                        404,
                        "Program was not found.");
                }

                if (await _dbContext.Tracks.AnyAsync(
                        x =>
                            x.ProgramId == request.ProgramId &&
                            x.Name == name,
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

    public async Task<Result<TrackDto>> UpdateTrackAsync(
        Guid trackId,
        UpdateTrackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (trackId == Guid.Empty)
        {
            return Result<TrackDto>.Failure(
                400,
                "A valid track ID is required.");
        }

        if (request is null)
        {
            return Result<TrackDto>.Failure(400, "Request body is required.");
        }

        string name = AdmissionConfigurationText.NormalizeRequired(request.Name);
        string? description = AdmissionConfigurationText.NormalizeOptional(
            request.Description);

        if (!AdmissionConfigurationText.IsRequiredValid(name, 200) ||
            description?.Length > 4000)
        {
            return Result<TrackDto>.Failure(
                400,
                "Track name is required and must not exceed 200 characters; description must not exceed 4000 characters.");
        }

        var topicValidation = TrackTopicValidator.Validate(
            request.PrerequisiteTopics);

        if (!topicValidation.IsValid)
        {
            return Result<TrackDto>.Failure(
                400,
                topicValidation.ErrorMessage);
        }

        return await _writeExecutor.ExecuteSerializableAsync(
            cancellationToken,
            async ct =>
            {
                var track = await _dbContext.Tracks.SingleOrDefaultAsync(
                    x => x.Id == trackId,
                    ct);

                if (track is null)
                {
                    return Result<TrackDto>.Failure(
                        404,
                        "Track was not found.");
                }

                if (await _applicationReader.IsTrackConfigurationLockedAsync(
                        trackId,
                        ct))
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

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    private static TrackDto MapTrack(
        Track track,
        IReadOnlyList<BranchOfferingDto> offerings)
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

    private static BranchOfferingDto MapBranchOffering(
        TrackBranchOffering offering)
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
}
