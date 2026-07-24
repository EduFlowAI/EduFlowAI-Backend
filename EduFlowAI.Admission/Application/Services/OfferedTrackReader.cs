using System.Linq.Expressions;
using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Admission.Application.Services;

public sealed class OfferedTrackReader : IOfferedTrackReader
{
    private readonly IAdmissionDbContext _dbContext;

    public OfferedTrackReader(IAdmissionDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OfferedTrackForRecommendationDto>>
        GetActiveOfferedTracksAsync(
            CancellationToken cancellationToken = default)
    {
        var offerings = await _dbContext.TrackBranchOfferings
            .AsNoTracking()
            .Include(offering => offering.Track)
            .Where(OfferedTrackRecommendationProjection.IsActiveOffering)
            .OrderBy(offering => offering.Track.Name)
            .ThenBy(offering => offering.TrackId)
            .ThenBy(offering => offering.BranchId)
            .ToListAsync(cancellationToken);

        return OfferedTrackRecommendationProjection.Project(offerings);
    }
}

internal static class OfferedTrackRecommendationProjection
{
    // TrackBranchOffering has no IsActive flag. An offering is available when its
    // cycle, track, and branch are all active/available.
    internal static readonly Expression<Func<TrackBranchOffering, bool>>
        IsActiveOffering = offering =>
            offering.Cycle.Status == CycleStatus.Active &&
            offering.Track.IsActive &&
            offering.Branch.IsActive;

    internal static IReadOnlyList<OfferedTrackForRecommendationDto> Project(
        IEnumerable<TrackBranchOffering> offerings)
    {
        ArgumentNullException.ThrowIfNull(offerings);

        var tracks = offerings
            .GroupBy(offering => offering.TrackId)
            .Select(group => group.First().Track)
            .OrderBy(track => track.Name)
            .ThenBy(track => track.Id)
            .Select(track => new OfferedTrackForRecommendationDto
            {
                TrackId = track.Id,
                Name = track.Name,
                Description = track.Description,
                PrerequisiteTopics = Array.AsReadOnly(
                    track.PrerequisiteTopics.ToArray())
            })
            .ToArray();

        return Array.AsReadOnly(tracks);
    }
}
