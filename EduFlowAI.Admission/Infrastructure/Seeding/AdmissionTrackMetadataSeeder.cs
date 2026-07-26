using System.Data;
using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduFlowAI.Admission.Infrastructure.Seeding;

internal static class AdmissionTrackMetadataSeeder
{
    private const long AdmissionSeedLockKey = 1_164_618_325_329_019_809;
    private const string NineMonthProgramCode = "9M";

    internal static async Task SeedAsync(
        IAdmissionDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IDbContextTransaction? transaction = null;

        try
        {
            if (context.Database.IsRelational())
            {
                transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                if (string.Equals(
                        context.Database.ProviderName,
                        "Npgsql.EntityFrameworkCore.PostgreSQL",
                        StringComparison.Ordinal))
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "SELECT pg_advisory_xact_lock({0});",
                        new object[] { AdmissionSeedLockKey },
                        cancellationToken);
                }
            }

            var programCandidates = await context.Programs
                .Where(program =>
                    program.Id == AdmissionSeedIds.NineMonthProgramId ||
                    program.Code.ToUpper() == NineMonthProgramCode)
                .ToListAsync(cancellationToken);

            var program = ResolveNineMonthProgram(programCandidates);

            Guid[] seedIds = AdmissionTrackSeedCatalog.All
                .Select(definition => definition.Id)
                .ToArray();

            var trackCandidates = await context.Tracks
                .Where(track =>
                    track.ProgramId == program.Id ||
                    seedIds.Contains(track.Id))
                .ToListAsync(cancellationToken);

            bool hasChanges = false;

            foreach (var definition in AdmissionTrackSeedCatalog.All)
            {
                var track = ResolveSeededTrack(
                    trackCandidates,
                    program.Id,
                    definition);

                hasChanges |= BackfillMissingMetadata(track, definition, now);
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
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

    internal static Program ResolveNineMonthProgram(
        IReadOnlyCollection<Program> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var idMatch = candidates.SingleOrDefault(
            program => program.Id == AdmissionSeedIds.NineMonthProgramId);

        if (idMatch is not null &&
            !StringComparer.OrdinalIgnoreCase.Equals(
                idMatch.Code,
                NineMonthProgramCode))
        {
            throw new InvalidOperationException(
                $"Admission track metadata seed conflict: program ID " +
                $"'{AdmissionSeedIds.NineMonthProgramId}' has code " +
                $"'{idMatch.Code}' instead of '{NineMonthProgramCode}'.");
        }

        var codeMatches = candidates
            .Where(program =>
                StringComparer.OrdinalIgnoreCase.Equals(
                    program.Code,
                    NineMonthProgramCode))
            .ToList();

        if (codeMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Admission track metadata seed found {codeMatches.Count} " +
                $"programs with code '{NineMonthProgramCode}'.");
        }

        var codeMatch = codeMatches.SingleOrDefault();

        if (idMatch is not null &&
            codeMatch is not null &&
            idMatch.Id != codeMatch.Id)
        {
            throw new InvalidOperationException(
                "Admission track metadata seed found different programs for " +
                $"the stable ID and code '{NineMonthProgramCode}'.");
        }

        return idMatch ?? codeMatch ?? throw new InvalidOperationException(
            $"Admission track metadata seed could not find program code " +
            $"'{NineMonthProgramCode}'.");
    }

    internal static Track ResolveSeededTrack(
        IReadOnlyCollection<Track> candidates,
        Guid programId,
        AdmissionTrackSeedDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(definition);

        var idMatch = candidates.SingleOrDefault(
            track => track.Id == definition.Id);

        if (idMatch is not null &&
            (idMatch.ProgramId != programId ||
             !StringComparer.OrdinalIgnoreCase.Equals(
                 idMatch.Name,
                 definition.Name)))
        {
            throw new InvalidOperationException(
                $"Admission track metadata seed conflict for track ID " +
                $"'{definition.Id}': the existing row has a different " +
                "program or name.");
        }

        var nameMatches = candidates
            .Where(track =>
                track.ProgramId == programId &&
                StringComparer.OrdinalIgnoreCase.Equals(
                    track.Name,
                    definition.Name))
            .ToList();

        if (nameMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Admission track metadata seed found duplicate tracks " +
                $"named '{definition.Name}' in program '{programId}'.");
        }

        var nameMatch = nameMatches.SingleOrDefault();

        if (idMatch is not null &&
            nameMatch is not null &&
            idMatch.Id != nameMatch.Id)
        {
            throw new InvalidOperationException(
                $"Admission track metadata seed found different tracks for " +
                $"stable ID '{definition.Id}' and name '{definition.Name}'.");
        }

        return idMatch ?? nameMatch ?? throw new InvalidOperationException(
            $"Admission track metadata could not find '{definition.Name}' " +
            $"in program '{programId}'.");
    }
}
