using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Admission.Infrastructure.Seeding;

internal static class AdmissionTrackMetadataSeeder
{
    internal static async Task SeedAsync(
        IAdmissionDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Guid[] seedIds = AdmissionTrackSeedCatalog.All
            .Select(definition => definition.Id)
            .ToArray();

        var candidates = await context.Tracks
            .Where(track =>
                track.ProgramId == AdmissionSeedIds.NineMonthProgramId ||
                seedIds.Contains(track.Id))
            .ToListAsync(cancellationToken);

        bool hasChanges = false;

        foreach (var definition in AdmissionTrackSeedCatalog.All)
        {
            var track = ResolveSeededTrack(candidates, definition);
            hasChanges |= BackfillMissingMetadata(track, definition, now);
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    internal static bool BackfillMissingMetadata(
        Track track,
        AdmissionTrackSeedDefinition definition,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(definition);

        bool changed = false;

        if (string.IsNullOrWhiteSpace(track.Description))
        {
            track.Description = definition.Description;
            changed = true;
        }

        if (track.PrerequisiteTopics.Count == 0)
        {
            track.PrerequisiteTopics = [.. definition.PrerequisiteTopics];
            changed = true;
        }

        if (changed)
        {
            track.UpdatedAt = now;
        }

        return changed;
    }

    private static Track ResolveSeededTrack(
        IReadOnlyCollection<Track> candidates,
        AdmissionTrackSeedDefinition definition)
    {
        var idMatch = candidates.SingleOrDefault(track => track.Id == definition.Id);
        if (idMatch is not null)
        {
            return idMatch;
        }

        var nameMatches = candidates
            .Where(track =>
                track.ProgramId == AdmissionSeedIds.NineMonthProgramId &&
                StringComparer.OrdinalIgnoreCase.Equals(track.Name, definition.Name))
            .ToList();

        return nameMatches.Count switch
        {
            1 => nameMatches[0],
            0 => throw new InvalidOperationException(
                $"Admission track seed metadata could not find '{definition.Name}'."),
            _ => throw new InvalidOperationException(
                $"Admission track seed metadata found duplicate tracks named '{definition.Name}'.")
        };
    }
}
