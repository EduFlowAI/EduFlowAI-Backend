using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Infrastructure.Caching;
using EduFlowAI.AI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly EmbeddingCacheService _cache;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(HttpClient httpClient, IOptions<GeminiOptions> options, EmbeddingCacheService cache, ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text content cannot be null or empty when generating an embedding.", nameof(text));


        if (_cache.TryGetEmbedding(text, out var cachedEmbedding))
        {
            return cachedEmbedding!;
        }

        string requestUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.EmbeddingModel}:embedContent?key={_options.ApiKey}";

        var payload = new
        {
            model = _options.EmbeddingModel,
            content = new
            {
                parts = new[]
                {
                    new { text = text }
                }
            },
            outputDimensionality = _options.EmbeddingDimensions
        };

        // One network blip shouldn't fail a whole multi-chunk document.
        const int maxAttempts = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<GeminiResponseContract>(cancellationToken: cancellationToken);

                if (result?.Embedding?.Values == null)
                {
                    throw new InvalidOperationException("The Gemini API returned a successful status code, but the embedding payload was empty or malformed.");
                }

                _cache.CacheEmbedding(text, result.Embedding.Values);

                return result.Embedding.Values;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Embedding attempt {Attempt} failed; retrying in {DelaySeconds}s", attempt, attempt * 2);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Failed to generate embedding after {maxAttempts} attempts due to a network error.", lastException);
    }

    #region Internal JSON Contract Classes

    private class GeminiResponseContract
    {
        [JsonPropertyName("embedding")]
        public EmbeddingData Embedding { get; set; } = null!;
    }

    private class EmbeddingData
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = null!;
    }

    #endregion
}