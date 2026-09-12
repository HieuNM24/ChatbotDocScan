using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pgvector;

namespace backend.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private readonly string _embeddingModel;
    private readonly string _chatModel;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured.");
        _embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "gemini-embedding-001";
        _chatModel = configuration["Gemini:ChatModel"] ?? "gemini-3.6-flash";
        _logger = logger;
    }

    /// <summary>
    /// Calls the Gemini Embedding API to get a float[] embedding for the given text.
    /// Configured with outputDimensionality = 768 to match database vector(768).
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var url = $"{BaseUrl}/models/{_embeddingModel}:embedContent?key={_apiKey}";

        var body = new
        {
            content = new
            {
                parts = new[] { new { text } }
            },
            outputDimensionality = 768
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini embedding API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Gemini embedding API returned {response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var valuesArray = doc.RootElement
            .GetProperty("embedding")
            .GetProperty("values");

        var floats = valuesArray.EnumerateArray()
            .Select(v => v.GetSingle())
            .ToArray();

        _logger.LogInformation("Embedding generated: {Dims} dimensions", floats.Length);
        return floats;
    }

    /// <summary>
    /// Calls the Gemini Chat API to generate an answer given a prompt and context.
    /// </summary>
    public async Task<string> GenerateAnswerAsync(string question, string context)
    {
        var prompt = $"""
            Bạn là một trợ lý AI chuyên nghiệp. Hãy trả lời câu hỏi dựa HOÀN TOÀN vào thông tin được cung cấp trong ngữ cảnh bên dưới.
            Nếu thông tin trong ngữ cảnh không đủ để trả lời, hãy nói rõ điều đó.
            Trả lời bằng ngôn ngữ của câu hỏi.

            === NGỮ CẢNH ===
            {context}

            === CÂU HỎI ===
            {question}

            === CÂU TRẢ LỜI ===
            """;

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 2048
            }
        };

        var candidateModels = new[] { _chatModel, "gemini-3.5-flash", "gemini-3.1-flash-lite" }.Distinct();
        Exception? lastException = null;

        foreach (var model in candidateModels)
        {
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var url = $"{BaseUrl}/models/{model}:generateContent?key={_apiKey}";
                    var json = JsonSerializer.Serialize(body);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseJson);

                        var answer = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString() ?? "Không thể tạo câu trả lời.";

                        return answer.Trim();
                    }

                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Model {Model} attempt {Attempt} returned {Status}: {Body}", model, attempt, response.StatusCode, errorBody);

                    // If 503/429 (high demand or rate limit), wait briefly and retry or try fallback
                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                    {
                        await Task.Delay(1000 * attempt);
                        continue;
                    }

                    throw new HttpRequestException($"Gemini chat API returned {response.StatusCode}: {errorBody}");
                }
                catch (Exception ex) when (ex is not HttpRequestException)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Error calling model {Model} on attempt {Attempt}", model, attempt);
                    await Task.Delay(1000);
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                }
            }
        }

        throw lastException ?? new Exception("Không thể kết nối đến Gemini Chat API sau nhiều lần thử.");
    }
}
