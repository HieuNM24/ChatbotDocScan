namespace backend.DTOs;

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
}

public class UploadResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
