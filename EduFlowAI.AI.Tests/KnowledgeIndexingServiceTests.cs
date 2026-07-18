using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Exceptions;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Domain.Entities;
using EduFlowAI.AI.Domain.Enums;
using EduFlowAI.AI.Infrastructure.Options;
using EduFlowAI.AI.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Tests;

// Covers the ingestion rules that callers rely on, independent of any HTTP host.
public class KnowledgeIndexingServiceTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\nbody");

    [Fact]
    public async Task AddDocument_rejects_an_empty_file()
    {
        var (service, _, _) = Build();

        await Assert.ThrowsAsync<KnowledgeBaseValidationException>(
            () => service.AddDocumentAsync(new MemoryStream(), "empty.pdf", 0));
    }

    [Fact]
    public async Task AddDocument_rejects_a_file_over_the_size_cap()
    {
        var (service, _, _) = Build(options => options.MaxUploadBytes = 10);

        await Assert.ThrowsAsync<KnowledgeBaseTooLargeException>(
            () => service.AddDocumentAsync(new MemoryStream(PdfBytes), "big.pdf", 11));
    }

    [Fact]
    public async Task AddDocument_rejects_a_non_pdf_renamed_to_pdf()
    {
        var (service, _, _) = Build();
        var disguised = new MemoryStream(Encoding.ASCII.GetBytes("PK not a pdf"));

        await Assert.ThrowsAsync<KnowledgeBaseValidationException>(
            () => service.AddDocumentAsync(disguised, "invoice.pdf", disguised.Length));
    }

    [Fact]
    public async Task AddDocument_indexes_a_pdf_and_stores_the_original()
    {
        var (service, repository, storage) = Build();

        var documentId = await service.AddDocumentAsync(new MemoryStream(PdfBytes), "track.pdf", PdfBytes.Length);

        var document = repository.Documents.Single();
        Assert.Equal(documentId, document.Id);
        Assert.Equal(KnowledgeBaseStatus.Indexed, document.Status);
        Assert.Equal($"knowledge-base/{documentId}.pdf", document.StorageKey);
        Assert.True(storage.Files.ContainsKey(document.StorageKey));
        Assert.NotEmpty(repository.Chunks);
    }

    [Fact]
    public async Task AddDocument_keeps_the_text_extension_for_text_files()
    {
        var (service, repository, _) = Build();
        var content = new MemoryStream(Encoding.UTF8.GetBytes("Admission opens in July."));

        var documentId = await service.AddDocumentAsync(content, "faq.md", content.Length);

        Assert.Equal($"knowledge-base/{documentId}.md", repository.Documents.Single().StorageKey);
    }

    [Fact]
    public async Task AddText_rejects_blank_content()
    {
        var (service, _, _) = Build();

        await Assert.ThrowsAsync<KnowledgeBaseValidationException>(() => service.AddTextAsync("Note", "   "));
    }

    [Fact]
    public async Task AddText_stores_a_txt_file_so_resync_can_rebuild_it()
    {
        var (service, repository, storage) = Build();

        var documentId = await service.AddTextAsync("Fees", "The application fee is 100 EGP.");

        var document = repository.Documents.Single();
        Assert.Equal("Fees.txt", document.FileName);
        Assert.Equal($"knowledge-base/{documentId}.txt", document.StorageKey);
        Assert.True(storage.Files.ContainsKey(document.StorageKey));
    }

    [Fact]
    public async Task AddText_falls_back_to_a_default_title()
    {
        var (service, repository, _) = Build();

        await service.AddTextAsync(null, "Some note.");

        Assert.Equal("Pasted note.txt", repository.Documents.Single().FileName);
    }

    [Fact]
    public async Task Delete_is_refused_while_a_resync_is_running()
    {
        var syncState = new KnowledgeBaseSyncState();
        var (service, _, _) = Build(syncState: syncState);
        syncState.TryBeginSync();

        await Assert.ThrowsAsync<KnowledgeBaseBusyException>(() => service.DeleteDocumentAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_removes_the_row_and_the_stored_file()
    {
        var (service, repository, storage) = Build();
        var documentId = await service.AddDocumentAsync(new MemoryStream(PdfBytes), "track.pdf", PdfBytes.Length);

        Assert.True(await service.DeleteDocumentAsync(documentId));
        Assert.Empty(repository.Documents);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task Delete_reports_a_missing_document()
    {
        var (service, _, _) = Build();

        Assert.False(await service.DeleteDocumentAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Resync_is_refused_while_another_one_is_running()
    {
        var syncState = new KnowledgeBaseSyncState();
        var (service, _, _) = Build(syncState: syncState);
        syncState.TryBeginSync();

        await Assert.ThrowsAsync<KnowledgeBaseBusyException>(() => service.ResyncAllAsync());
    }

    [Fact]
    public async Task Resync_releases_the_gate_so_a_later_one_can_run()
    {
        var syncState = new KnowledgeBaseSyncState();
        var (service, _, _) = Build(syncState: syncState);

        await service.ResyncAllAsync();

        Assert.False(syncState.IsSyncing);
    }

    [Fact]
    public async Task Resync_rebuilds_every_chunk_from_storage()
    {
        var (service, repository, _) = Build();
        await service.AddDocumentAsync(new MemoryStream(PdfBytes), "track.pdf", PdfBytes.Length);
        var firstPass = repository.Chunks.Count;

        var result = await service.ResyncAllAsync();

        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(1, result.Indexed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(firstPass, repository.Chunks.Count);
    }

    [Fact]
    public async Task Resync_marks_a_document_failed_when_its_file_is_gone()
    {
        var (service, repository, storage) = Build();
        await service.AddDocumentAsync(new MemoryStream(PdfBytes), "track.pdf", PdfBytes.Length);
        storage.Files.Clear();

        var result = await service.ResyncAllAsync();

        Assert.Equal(1, result.Failed);
        Assert.Equal(KnowledgeBaseStatus.Failed, repository.Documents.Single().Status);
    }

    [Fact]
    public async Task Status_reports_the_chunk_count()
    {
        var (service, repository, _) = Build();
        var documentId = await service.AddDocumentAsync(new MemoryStream(PdfBytes), "track.pdf", PdfBytes.Length);

        var status = await service.GetDocumentStatusAsync(documentId);

        Assert.NotNull(status);
        Assert.Equal("Indexed", status!.Status);
        Assert.Equal(repository.Chunks.Count, status.ChunkCount);
    }

    [Fact]
    public async Task Status_is_null_for_an_unknown_document()
    {
        var (service, _, _) = Build();

        Assert.Null(await service.GetDocumentStatusAsync(Guid.NewGuid()));
    }

    private static (KnowledgeBaseService service, FakeRepository repository, FakeFileStorage storage) Build(
        Action<IngestionOptions>? configure = null,
        KnowledgeBaseSyncState? syncState = null)
    {
        var options = new IngestionOptions();
        configure?.Invoke(options);

        var repository = new FakeRepository();
        var storage = new FakeFileStorage();

        var service = new KnowledgeBaseService(
            new FakeTextExtractor(),
            new FakeEmbeddingService(),
            repository,
            storage,
            syncState ?? new KnowledgeBaseSyncState(),
            Options.Create(options),
            NullLogger<KnowledgeBaseService>.Instance);

        return (service, repository, storage);
    }

    private sealed class FakeTextExtractor : IDocumentTextExtractor
    {
        public Task<string> ExtractTextAsync(byte[] content, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult("The .NET Enterprise Solutions track runs for nine months at ITI.");
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new float[] { text.Length, 0f, 1f });
    }

    private sealed class FakeFileStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        public async Task<string> SaveAsync(Stream content, string relativeKey, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Files[relativeKey] = buffer.ToArray();
            return relativeKey;
        }

        public Task<Stream> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default)
        {
            if (!Files.TryGetValue(relativeKey, out var bytes))
                throw new FileNotFoundException(relativeKey);

            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public Task DeleteAsync(string relativeKey, CancellationToken cancellationToken = default)
        {
            Files.Remove(relativeKey);
            return Task.CompletedTask;
        }

        public bool Exists(string relativeKey) => Files.ContainsKey(relativeKey);
    }

    private sealed class FakeRepository : IKnowledgeRepository
    {
        public List<KnowledgeBaseDocument> Documents { get; } = new();
        public List<KnowledgeBaseChunk> Chunks { get; } = new();

        public Task<KnowledgeBaseDocument?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.FirstOrDefault(d => d.Id == id));

        public Task<List<KnowledgeBaseDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.ToList());

        public Task AddDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Add(document);
            return Task.CompletedTask;
        }

        public Task UpdateDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteDocumentAsync(KnowledgeBaseDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Remove(document);
            Chunks.RemoveAll(c => c.KnowledgeBaseDocumentId == document.Id);
            return Task.CompletedTask;
        }

        public Task AddChunksAsync(List<KnowledgeBaseChunk> chunks, CancellationToken cancellationToken = default)
        {
            Chunks.AddRange(chunks);
            return Task.CompletedTask;
        }

        public Task<int> GetChunkCountAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Chunks.Count(c => c.KnowledgeBaseDocumentId == documentId));

        public Task DeleteAllChunksAsync(CancellationToken cancellationToken = default)
        {
            Chunks.Clear();
            return Task.CompletedTask;
        }

        public Task<List<RetrievedChunkDto>> SearchChunksByEmbeddingAsync(
            Pgvector.Vector embedding,
            int limit = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<RetrievedChunkDto>());
    }
}
