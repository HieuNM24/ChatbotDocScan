using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdfService;
    private readonly GeminiService _geminiService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        AppDbContext db,
        PdfService pdfService,
        GeminiService geminiService,
        ILogger<DocumentsController> logger)
    {
        _db = db;
        _pdfService = pdfService;
        _geminiService = geminiService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/documents/upload
    /// Receives a PDF file, extracts text, chunks it, generates embeddings, saves to DB.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB limit
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Không có tệp nào được tải lên." });

        if (file.Length > 20 * 1024 * 1024)
            return BadRequest(new { message = "Dung lượng tệp vượt quá giới hạn cho phép (20MB)." });

        // 1. Kiểm tra phần mở rộng file (.pdf)
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return BadRequest(new { message = "Chỉ chấp nhận tệp có định dạng .pdf." });

        // 2. Kiểm tra Content-Type
        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "MIME Type không hợp lệ. Tệp phải là PDF." });

        // 3. Kiểm tra Magic Bytes (%PDF- tương đương hex 25 50 44 46)
        using (var reader = new BinaryReader(file.OpenReadStream()))
        {
            var headerBytes = reader.ReadBytes(4);
            var isPdfMagic = headerBytes.Length == 4
                && headerBytes[0] == 0x25
                && headerBytes[1] == 0x50
                && headerBytes[2] == 0x44
                && headerBytes[3] == 0x46;

            if (!isPdfMagic)
                return BadRequest(new { message = "Nội dung tệp không phải định dạng PDF hợp lệ (Magic Bytes mismatch)." });
        }

        _logger.LogInformation("Uploading document: {FileName} ({Size} bytes)", file.FileName, file.Length);

        // 1. Extract text
        string text;
        using (var stream = file.OpenReadStream())
        {
            text = _pdfService.ExtractText(stream);
        }

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { message = "Could not extract any text from the PDF." });

        // 2. Chunk text
        var chunks = _pdfService.ChunkText(text);

        if (chunks.Count == 0)
            return BadRequest(new { message = "Document produced no chunks." });

        // 3. Create Document entity
        var document = new Document
        {
            FileName = file.FileName,
            UploadedAt = DateTime.UtcNow
        };
        _db.Documents.Add(document);

        // 4. Generate embeddings for each chunk and save
        var documentChunks = new List<DocumentChunk>();

        for (int i = 0; i < chunks.Count; i++)
        {
            _logger.LogInformation("Generating embedding for chunk {Index}/{Total}", i + 1, chunks.Count);

            var embedding = await _geminiService.GetEmbeddingAsync(chunks[i]);

            documentChunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                Document = document,
                Content = chunks[i],
                Embedding = new Vector(embedding)
            });

            // Small delay to avoid rate limiting
            if (i < chunks.Count - 1)
                await Task.Delay(200);
        }

        _db.DocumentChunks.AddRange(documentChunks);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Document {Id} saved with {Count} chunks.", document.Id, documentChunks.Count);

        return Ok(new
        {
            documentId = document.Id,
            fileName = document.FileName,
            chunkCount = documentChunks.Count,
            message = $"Document uploaded and indexed successfully with {documentChunks.Count} chunks."
        });
    }

    /// <summary>
    /// GET /api/documents
    /// Returns a list of all uploaded documents.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var docs = await _db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.UploadedAt,
                ChunkCount = d.Chunks.Count
            })
            .ToListAsync();

        return Ok(docs);
    }

    /// <summary>
    /// DELETE /api/documents/{id}
    /// Deletes a document and all its chunks.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Document not found." });

        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Document deleted successfully." });
    }
}
