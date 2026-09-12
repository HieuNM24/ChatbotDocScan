using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace backend.Services;

public class PdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads all text from a PDF file stream.
    /// </summary>
    public string ExtractText(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var sb = new System.Text.StringBuilder();

        foreach (Page page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Splits text into chunks of approximately <paramref name="wordsPerChunk"/> words,
    /// with an optional overlap to preserve context at boundaries.
    /// </summary>
    public List<string> ChunkText(string text, int wordsPerChunk = 300, int overlapWords = 30)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        int step = Math.Max(1, wordsPerChunk - overlapWords);

        for (int i = 0; i < words.Length; i += step)
        {
            var chunkWords = words.Skip(i).Take(wordsPerChunk).ToArray();
            var chunk = string.Join(' ', chunkWords).Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            // Stop if we covered all words
            if (i + wordsPerChunk >= words.Length)
                break;
        }

        _logger.LogInformation("Text chunked into {Count} chunks (~{Words} words each)", chunks.Count, wordsPerChunk);
        return chunks;
    }
}
