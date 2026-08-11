using System.Linq.Expressions;
using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Admission.Infrastructure.Seeding;
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
                .ThenInclude(track => track.Program)
            .Include(offering => offering.Branch)
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
            .Where(IsAllowedOffering)
            .GroupBy(offering => offering.TrackId)
            .Select(group => group.First().Track)
            .OrderBy(track => track.Name)
            .ThenBy(track => track.Id)
            .Select(track =>
            {
                bool isNineMonthProgram = StringComparer.OrdinalIgnoreCase.Equals(
                    track.Program?.Code,
                    "9M");
                var definition = isNineMonthProgram
                    ? AdmissionTrackSeedCatalog.Find(track.Id, track.Name)
                    : null;

                return new OfferedTrackForRecommendationDto
                {
                    TrackId = track.Id,
                    Name = definition?.Name ?? track.Name,
                    Description = definition is null
                        ? track.Description
                        : definition.Description,
                    PrerequisiteTopics = Array.AsReadOnly(
                        (definition?.PrerequisiteTopics ?? track.PrerequisiteTopics)
                        .ToArray()),
                    Category = definition?.Category,
                    MinimumGrade = definition?.MinimumGrade,
                    EligibilitySummary = definition?.EligibilitySummary,
                    TotalHours = definition?.TotalHours,
                    GraduationYearLimitYears =
                        definition?.MaxYearsSinceGraduation,
                    Locations = Array.AsReadOnly(
                        (definition?.Locations ?? Array.Empty<string>())
                        .ToArray())
                };
            })
            .ToArray();

        return Array.AsReadOnly(tracks);
    }

    internal static bool IsAllowedOffering(TrackBranchOffering offering)
    {
        string? programCode = offering.Track.Program?.Code;
        if (!StringComparer.OrdinalIgnoreCase.Equals(programCode, "9M"))
        {
            return true;
        }

        var definition = AdmissionTrackSeedCatalog.Find(
            offering.Track.Id,
            offering.Track.Name);

        return definition is not null &&
            AdmissionTrackSeedCatalog.IsCanonicalLocation(
                definition,
                offering.Branch.Name);
    }
}
