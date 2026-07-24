using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.Admission.Tests;

public sealed class OfferedTrackReaderTests
{
    [Fact]
    public void Eligibility_requires_active_cycle_track_and_branch()
    {
        var predicate =
            OfferedTrackRecommendationProjection.IsActiveOffering.Compile();

        Assert.True(predicate(CreateOffering(
            CycleStatus.Active,
            trackIsActive: true,
            branchIsActive: true)));

        Assert.False(predicate(CreateOffering(
            CycleStatus.Draft,
            trackIsActive: true,
            branchIsActive: true)));

        Assert.False(predicate(CreateOffering(
            CycleStatus.Active,
            trackIsActive: false,
            branchIsActive: true)));

        Assert.False(predicate(CreateOffering(
            CycleStatus.Active,
            trackIsActive: true,
            branchIsActive: false)));
    }

    [Fact]
    public void Projection_removes_duplicates_and_returns_read_only_dtos()
    {
        var aiTrack = CreateTrack(
            "AI and Machine Learning",
            ["Python", "Linear Algebra"]);
        var cloudTrack = CreateTrack(
            "Cloud Architecture",
            ["Linux", "Networking"]);

        var offerings = new[]
        {
            CreateOffering(
                CycleStatus.Active,
                trackIsActive: true,
                branchIsActive: true,
                track: cloudTrack),
            CreateOffering(
                CycleStatus.Active,
                trackIsActive: true,
                branchIsActive: true,
                track: aiTrack),
            CreateOffering(
                CycleStatus.Active,
                trackIsActive: true,
                branchIsActive: true,
                track: aiTrack)
        };

        var result = OfferedTrackRecommendationProjection.Project(offerings);

        Assert.Equal(2, result.Count);
        Assert.Equal(aiTrack.Id, result[0].TrackId);
        Assert.Equal(cloudTrack.Id, result[1].TrackId);
        Assert.Equal(
            new[] { "Python", "Linear Algebra" },
            result[0].PrerequisiteTopics);

        var outerCollection = Assert.IsAssignableFrom<
            IList<OfferedTrackForRecommendationDto>>(result);
        Assert.Throws<NotSupportedException>(
            () => outerCollection.Add(result[0]));

        var topics = Assert.IsAssignableFrom<ICollection<string>>(
            result[0].PrerequisiteTopics);
        Assert.Throws<NotSupportedException>(() => topics.Add("Statistics"));
    }

    private static Track CreateTrack(
        string name,
        List<string> prerequisiteTopics)
    {
        return new Track
        {
            Id = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
            Name = name,
            Description = $"{name} description",
            PrerequisiteTopics = prerequisiteTopics,
            IsActive = true
        };
    }

    private static TrackBranchOffering CreateOffering(
        CycleStatus cycleStatus,
        bool trackIsActive,
        bool branchIsActive,
        Track? track = null)
    {
        track ??= CreateTrack(
            "Test Track",
            ["Programming fundamentals"]);
        track.IsActive = trackIsActive;

        var cycle = new AdmissionCycle
        {
            Id = Guid.NewGuid(),
            ProgramId = track.ProgramId,
            Label = "Test Cycle",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DeadlineUtc = DateTimeOffset.UtcNow.AddMonths(1),
            Status = cycleStatus
        };

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Test Branch",
            IsActive = branchIsActive
        };

        return new TrackBranchOffering
        {
            Id = Guid.NewGuid(),
            CycleId = cycle.Id,
            TrackId = track.Id,
            BranchId = branch.Id,
            Capacity = 25,
            Cycle = cycle,
            Track = track,
            Branch = branch
        };
    }
}
