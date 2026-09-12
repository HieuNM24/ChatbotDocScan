using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChatLog> ChatLogs => Set<ChatLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Document
        modelBuilder.Entity<Document>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.FileName).IsRequired();
            e.HasMany(d => d.Chunks)
             .WithOne(c => c.Document)
             .HasForeignKey(c => c.DocumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // DocumentChunk
        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Content).IsRequired();
            // Store embedding as vector(768) - text-embedding-004 produces 768 dims
            e.Property(c => c.Embedding).HasColumnType("vector(768)");
            e.HasIndex(c => c.Embedding)
             .HasMethod("hnsw")
             .HasOperators("vector_cosine_ops")
             .HasStorageParameter("m", 16)
             .HasStorageParameter("ef_construction", 64);
        });

        // ChatLog
        modelBuilder.Entity<ChatLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Question).IsRequired();
            e.Property(l => l.Answer).IsRequired();
        });
    }
}
