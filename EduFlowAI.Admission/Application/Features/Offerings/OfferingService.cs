using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Features.Configuration.Common;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EduFlowAI.Admission.Application.Features.Offerings;

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

public interface IOfferingService
{
    Task<Result<IReadOnlyList<OfferingDto>>> ConfigureOfferingsAsync(
        Guid cycleId,
        ConfigureOfferingsRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class OfferingService : IOfferingService
{
    private readonly IAdmissionDbContext _dbContext;
    private readonly IAdmissionApplicationReader _applicationReader;
    private readonly TimeProvider _timeProvider;
    private readonly AdmissionWriteExecutor _writeExecutor;
    private readonly CycleConfigurationGuard _cycleGuard;

    public OfferingService(
        IAdmissionDbContext dbContext,
        IAdmissionApplicationReader applicationReader,
        TimeProvider timeProvider,
        AdmissionWriteExecutor writeExecutor,
        CycleConfigurationGuard cycleGuard)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(applicationReader);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(writeExecutor);
        ArgumentNullException.ThrowIfNull(cycleGuard);

        _dbContext = dbContext;
        _applicationReader = applicationReader;
        _timeProvider = timeProvider;
        _writeExecutor = writeExecutor;
        _cycleGuard = cycleGuard;
    }

    public async Task<Result<IReadOnlyList<OfferingDto>>> ConfigureOfferingsAsync(
        Guid cycleId,
        ConfigureOfferingsRequest request,
        CancellationToken cancellationToken = default)
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
            return Result<IReadOnlyList<OfferingDto>>.Failure(
                400,
                "At least one track/branch offering is required.");
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
            return Result<IReadOnlyList<OfferingDto>>.Failure(
                400,
                "Every offering requires a valid track, branch, and positive capacity.");
        }

        if (request.Offerings
            .GroupBy(x => new { x.TrackId, x.BranchId })
            .Any(group => group.Count() > 1))
        {
            return Result<IReadOnlyList<OfferingDto>>.Failure(
                400,
                "Duplicate track/branch combinations are not allowed.");
        }

        return await _writeExecutor.ExecuteSerializableAsync(
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

                var configurationError = await _cycleGuard.ValidateAsync(cycle, ct);
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

                if (tracks.Count != trackIds.Count ||
                    branches.Count != branchIds.Count)
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
                    offering => new OfferingKey(
                        offering.TrackId,
                        offering.BranchId));

                var requestedKeys = request.Offerings
                    .Select(input => new OfferingKey(
                        input.TrackId,
                        input.BranchId))
                    .ToHashSet();

                var removed = existing
                    .Where(offering =>
                        !requestedKeys.Contains(new OfferingKey(
                            offering.TrackId,
                            offering.BranchId)))
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

                var now = UtcNow;

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
                    .Select(x => OfferingMapper.Map(
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

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    private readonly record struct OfferingKey(
        Guid TrackId,
        Guid BranchId);
}

internal static class OfferingMapper
{
    public static OfferingDto Map(
        TrackBranchOffering offering,
        string trackName,
        string branchName)
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
}
