using AdmissionApplication = EduFlowAI.Admission.Domain.Entities.Application;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Application.Services;
using EduFlowAI.Documents.Domain.Entities;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DocumentStatus = EduFlowAI.Documents.Domain.Enums.DocumentStatus;
using DocumentType = EduFlowAI.Documents.Domain.Enums.DocumentType;
using IdentityGender = EduFlowAI.Identity.Domain.Enums.Gender;

namespace EduFlowAI.Documents.Tests;

public sealed class ApplicantDocumentServiceTests
{
    [Fact]
    public async Task Empty_file_is_rejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new FakeStorage(), "owner");

        var result = await service.UploadAsync(
            Guid.NewGuid(), DocumentType.NationalId, FormFile([], "id.pdf"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Non_owner_cannot_upload()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        var service = CreateService(db, new FakeStorage(), "other-user");

        var result = await service.UploadAsync(
            applicationId, DocumentType.NationalId, ValidPdf(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Invalid_application_state_blocks_upload()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.UnderDocumentVerification);
        var service = CreateService(db, new FakeStorage(), "owner");

        var result = await service.UploadAsync(
            applicationId, DocumentType.NationalId, ValidPdf(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Unsupported_document_type_is_rejected()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        var service = CreateService(db, new FakeStorage(), "owner", DocumentType.NationalId);

        var result = await service.UploadAsync(
            applicationId, DocumentType.BirthCertificate, ValidPdf(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task First_upload_saves_row_without_exposing_storage_key()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        var storage = new FakeStorage("secure-key.pdf");
        var service = CreateService(db, storage, "owner");

        var result = await service.UploadAsync(
            applicationId, DocumentType.NationalId, ValidPdf("..\\unsafe.pdf"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("unsafe.pdf", result.Data.OriginalFileName);
        Assert.DoesNotContain("secure-key", result.Data.ToString());
        var row = await db.ApplicantDocuments.SingleAsync();
        Assert.Equal("secure-key.pdf", row.StorageKey);
    }

    [Fact]
    public async Task Replacement_updates_same_row_then_deletes_old_file()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        var documentId = Guid.NewGuid();
        db.ApplicantDocuments.Add(new ApplicantDocument
        {
            Id = documentId,
            ApplicationId = applicationId,
            DocumentType = DocumentType.NationalId,
            StorageKey = "old.pdf",
            OriginalFileName = "old.pdf",
            Status = DocumentStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var storage = new FakeStorage("new.pdf");
        var service = CreateService(db, storage, "owner");

        var result = await service.UploadAsync(
            applicationId, DocumentType.NationalId, ValidPdf("new.pdf"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(documentId, result.Data.Id);
        Assert.Equal("new.pdf", (await db.ApplicantDocuments.SingleAsync()).StorageKey);
        Assert.Contains("old.pdf", storage.DeletedKeys);
        Assert.DoesNotContain("new.pdf", storage.DeletedKeys);
    }

    [Fact]
    public async Task Replacement_after_verification_is_blocked()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        db.ApplicantDocuments.Add(Document(applicationId, DocumentStatus.Verifying));
        await db.SaveChangesAsync();
        var storage = new FakeStorage();
        var service = CreateService(db, storage, "owner");

        var result = await service.UploadAsync(
            applicationId, DocumentType.NationalId, ValidPdf(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, storage.SaveCount);
    }

    [Fact]
    public async Task New_file_is_deleted_when_database_save_fails()
    {
        await using var db = CreateDb<FailingDbContext>();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        db.FailSaves = true;
        var storage = new FakeStorage("new.pdf");
        var service = CreateService(db, storage, "owner");

        var result = await service.UploadAsync(
            applicationId, DocumentType.NationalId, ValidPdf(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("new.pdf", storage.DeletedKeys);
    }

    [Fact]
    public async Task Listing_includes_uploaded_and_missing_required_documents()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        db.ApplicantDocuments.Add(Document(applicationId, DocumentStatus.Uploaded));
        await db.SaveChangesAsync();
        var service = CreateService(
            db, new FakeStorage(), "owner", DocumentType.NationalId, DocumentType.BirthCertificate);

        var result = await service.ListAsync(applicationId, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data.Documents.Count);
        Assert.Contains(result.Data.Documents, item => item.DocumentType == DocumentType.NationalId && item.IsUploaded);
        Assert.Contains(result.Data.Documents, item => item.DocumentType == DocumentType.BirthCertificate && !item.IsUploaded);
        Assert.DoesNotContain("storage.pdf", result.Data.ToString());
    }

    [Fact]
    public async Task Missing_requirements_block_submission()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        var service = CreateService(
            db, new FakeStorage(), "owner", DocumentType.NationalId, DocumentType.BirthCertificate);

        var result = await service.SubmitAsync(applicationId, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(2, result.Data.MissingDocumentTypes.Count);
    }

    [Fact]
    public async Task Complete_package_fails_closed_when_outbox_is_not_configured()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        db.ApplicantDocuments.Add(Document(applicationId, DocumentStatus.Uploaded));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeStorage(), "owner");

        var result = await service.SubmitAsync(applicationId, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal(ApplicationStatus.DocumentsRequired, (await db.Applications.SingleAsync()).Status);
        Assert.Equal(DocumentStatus.Uploaded, (await db.ApplicantDocuments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Owner_can_download_but_another_applicant_cannot()
    {
        await using var db = CreateDb();
        var applicationId = await SeedApplicationAsync(db, "owner", ApplicationStatus.DocumentsRequired);
        var document = Document(applicationId, DocumentStatus.Uploaded);
        db.ApplicantDocuments.Add(document);
        await db.SaveChangesAsync();
        var storage = new FakeStorage { ReadStream = new MemoryStream([1, 2, 3]) };

        var ownerResult = await CreateService(db, storage, "owner").OpenFileAsync(document.Id, default);
        var otherResult = await CreateService(db, storage, "other").OpenFileAsync(document.Id, default);

        Assert.True(ownerResult.IsSuccess);
        Assert.Equal("application/pdf", ownerResult.Data.ContentType);
        Assert.False(otherResult.IsSuccess);
        Assert.Equal(403, otherResult.StatusCode);
        Assert.Equal(1, storage.OpenCount);
        await ownerResult.Data.Stream.DisposeAsync();
    }

    [Fact]
    public async Task Missing_document_returns_not_found_without_opening_storage()
    {
        await using var db = CreateDb();
        var storage = new FakeStorage();

        var result = await CreateService(db, storage, "owner").OpenFileAsync(Guid.NewGuid(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(0, storage.OpenCount);
    }

    private static ApplicantDocumentService CreateService(
        EduFlowAIDbContext db,
        FakeStorage storage,
        string userId,
        params DocumentType[] requiredTypes) =>
        new(
            db,
            db,
            db,
            new FakeRequirementReader(requiredTypes.Length == 0 ? [DocumentType.NationalId] : requiredTypes),
            storage,
            new FakeCurrentUser(userId),
            NullLogger<ApplicantDocumentService>.Instance);

    private static EduFlowAIDbContext CreateDb() => CreateDb<TestDbContext>();

    private static TContext CreateDb<TContext>() where TContext : EduFlowAIDbContext
    {
        var options = new DbContextOptionsBuilder<EduFlowAIDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    private static async Task<Guid> SeedApplicationAsync(
        EduFlowAIDbContext db,
        string userId,
        ApplicationStatus status)
    {
        var applicationId = Guid.NewGuid();
        var offeringId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        db.Applications.Add(new AdmissionApplication
        {
            Id = applicationId,
            ApplicantUserId = userId,
            CycleId = Guid.NewGuid(),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.ApplicantProfiles.Add(new ApplicantProfile
        {
            Id = Guid.NewGuid(), UserId = userId, FullNameEn = "Owner", FullNameAr = "Owner",
            NationalId = "123", Nationality = "Egyptian", DateOfBirth = new DateOnly(2000, 1, 1),
            Gender = IdentityGender.Male, University = "U", Faculty = "F", DegreeLevel = "BSc",
            Major = "CS", GraduationYear = 2022
        });
        db.Tracks.Add(new Track
        {
            Id = trackId, ProgramId = Guid.NewGuid(), Name = "Track"
        });
        db.TrackBranchOfferings.Add(new TrackBranchOffering
        {
            Id = offeringId, TrackId = trackId, CycleId = Guid.NewGuid(), BranchId = Guid.NewGuid(), Capacity = 1
        });
        db.ApplicationPreferences.Add(new ApplicationPreference
        {
            Id = Guid.NewGuid(), ApplicationId = applicationId, TrackBranchOfferingId = offeringId, Rank = 1
        });
        await db.SaveChangesAsync();
        return applicationId;
    }

    private static ApplicantDocument Document(Guid applicationId, DocumentStatus status) => new()
    {
        Id = Guid.NewGuid(), ApplicationId = applicationId, DocumentType = DocumentType.NationalId,
        StorageKey = "storage.pdf", OriginalFileName = "id.pdf", Status = status,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private static IFormFile ValidPdf(string name = "id.pdf") => FormFile([0x25, 0x50, 0x44, 0x46, 1], name);

    private static IFormFile FormFile(byte[] bytes, string name) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    private sealed record FakeCurrentUser(string? UserId) : ICurrentUserService;

    private sealed class FakeRequirementReader(IReadOnlySet<DocumentType> types) : IProgramRequirementReader
    {
        public FakeRequirementReader(IEnumerable<DocumentType> types) : this(types.ToHashSet()) { }

        public Task<IReadOnlySet<DocumentType>> GetRequiredTypesAsync(
            Guid programId, string gender, CancellationToken cancellationToken) => Task.FromResult(types);
    }

    private sealed class FakeStorage(params string[] keys) : IFileStorageService
    {
        private readonly Queue<string> _keys = new(keys.Length == 0 ? ["new.pdf"] : keys);
        public List<string> DeletedKeys { get; } = [];
        public int SaveCount { get; private set; }
        public int OpenCount { get; private set; }
        public Stream ReadStream { get; set; } = new MemoryStream([1]);

        public Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(_keys.Dequeue());
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult(ReadStream);
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private class TestDbContext(DbContextOptions<EduFlowAIDbContext> options) : EduFlowAIDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<KnowledgeBaseChunk>();
            modelBuilder.Ignore<KnowledgeBaseDocument>();
        }
    }

    private sealed class FailingDbContext(DbContextOptions<EduFlowAIDbContext> options) : TestDbContext(options)
    {
        public bool FailSaves { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            FailSaves
                ? Task.FromException<int>(new DbUpdateException("Simulated failure."))
                : base.SaveChangesAsync(cancellationToken);
    }
}
