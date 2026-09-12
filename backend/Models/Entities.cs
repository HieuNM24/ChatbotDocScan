using Pgvector;
using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    [Required]
    public string Content { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = null!;
}

public class ChatLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Question { get; set; } = string.Empty;

    [Required]
    public string Answer { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
