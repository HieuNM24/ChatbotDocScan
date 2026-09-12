using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GeminiService _geminiService;
    private readonly ILogger<ChatController> _logger;
    private const int TopK = 3;

    public ChatController(
        AppDbContext db,
        GeminiService geminiService,
        ILogger<ChatController> logger)
    {
        _db = db;
        _geminiService = geminiService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/chat
    /// Receives a question, finds the top-K most relevant chunks via cosine similarity,
    /// builds a prompt, calls Gemini, saves the chat log, and returns the answer.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question cannot be empty." });

        _logger.LogInformation("Chat request: {Question}", request.Question);

        // 1. Get embedding for the question
        var questionEmbedding = await _geminiService.GetEmbeddingAsync(request.Question);
        var queryVector = new Vector(questionEmbedding);

        // 2. Query top-K chunks using cosine distance (<-> operator from pgvector)
        var topChunks = await _db.DocumentChunks
            .OrderBy(c => c.Embedding.CosineDistance(queryVector))
            .Take(TopK)
            .Select(c => c.Content)
            .ToListAsync();

        if (topChunks.Count == 0)
        {
            return Ok(new ChatResponse
            {
                Answer = "Hiện tại chưa có tài liệu nào trong hệ thống. Vui lòng tải lên tài liệu trước khi đặt câu hỏi."
            });
        }

        // 3. Build context from top chunks
        var context = string.Join("\n\n---\n\n", topChunks);
        _logger.LogInformation("Using {Count} chunks as context (total {Len} chars)", topChunks.Count, context.Length);

        // 4. Generate answer
        var answer = await _geminiService.GenerateAnswerAsync(request.Question, context);

        // 5. Save to ChatLog
        var chatLog = new ChatLog
        {
            Question = request.Question,
            Answer = answer,
            CreatedAt = DateTime.UtcNow
        };
        _db.ChatLogs.Add(chatLog);
        await _db.SaveChangesAsync();

        return Ok(new ChatResponse { Answer = answer });
    }

    /// <summary>
    /// GET /api/chat/history
    /// Returns the last 50 chat logs (newest first).
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var logs = await _db.ChatLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .Select(l => new
            {
                l.Id,
                l.Question,
                l.Answer,
                l.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// DELETE /api/chat/history
    /// Clears all chat logs.
    /// </summary>
    [HttpDelete("history")]
    public async Task<IActionResult> ClearHistory()
    {
        _db.ChatLogs.RemoveRange(_db.ChatLogs);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Chat history cleared successfully." });
    }
}
