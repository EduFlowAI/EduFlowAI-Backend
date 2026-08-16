using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.Documents.Application.Interfaces;

namespace EduFlowAI.Documents.Infrastructure.Storage
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private const int MaxFileSizeBytes = 10 * 1024 * 1024;

        private static readonly Dictionary<string, byte[][]> AllowedMagicBytes = new()
        {
            { ".pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } },
            { ".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } }
        };

        public FileStorageService(IAmazonS3 s3Client, IOptions<DocumentStorageOptions> options)
        {
            _s3Client = s3Client;
            _bucketName = options.Value.BucketName;
        }

        public async Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File cannot be null or empty.");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("File size exceeds the 10 MB limit.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedMagicBytes.ContainsKey(extension))
                throw new InvalidOperationException("Invalid file type. Only PDF, JPG, JPEG, and PNG are allowed.");

            using var stream = file.OpenReadStream();
            if (!await IsValidFileSignatureAsync(stream, extension, cancellationToken))
                throw new InvalidOperationException("File signature mismatch. The file might be corrupted or spoofed.");

            stream.Position = 0;

            var secureFileName = $"{Guid.NewGuid()}{extension}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = secureFileName,
                InputStream = stream,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            return secureFileName;
        }

        public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            var safeKey = ValidateStorageKey(storageKey);

            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = safeKey
            }, cancellationToken);
        }

        public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            var safeKey = ValidateStorageKey(storageKey);

            try
            {
                var response = await _s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = safeKey
                }, cancellationToken);

                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException("The requested file was not found.");
            }
        }

        private async Task<bool> IsValidFileSignatureAsync(Stream stream, string extension, CancellationToken cancellationToken)
        {
            var allowedSignatures = AllowedMagicBytes[extension];
            var maxSignatureLength = allowedSignatures.Max(s => s.Length);

            var headerBytes = new byte[maxSignatureLength];
            var bytesRead = await stream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length), cancellationToken);

            return allowedSignatures.Any(signature =>
                bytesRead >= signature.Length && headerBytes.Take(signature.Length).SequenceEqual(signature));
        }

        private static string ValidateStorageKey(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
                throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));

            var safeFileName = Path.GetFileName(storageKey);
            if (!string.Equals(safeFileName, storageKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid storage key.");

            return safeFileName;
        }
    }
}