using Microsoft.EntityFrameworkCore;
using Volt.Application.Interfaces;
using Volt.Infrastructure.Data;

namespace Volt.API.Services;

/// <summary>Processes a small DOCX batch after startup and then every five minutes.</summary>
public sealed class ProjectAttachmentDocumentExtractionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectAttachmentDocumentExtractionService> _logger;

    public ProjectAttachmentDocumentExtractionService(IServiceScopeFactory scopeFactory, ILogger<ProjectAttachmentDocumentExtractionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception error) { _logger.LogWarning(error, "Tracked-project DOCX extraction batch failed"); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var extractor = scope.ServiceProvider.GetRequiredService<IProjectAttachmentDocumentExtractor>();
        var attachments = await db.AdminTrackedProjectAttachments
            .Where(x => x.IsActive && x.DocumentExtractionStatus == "Pending")
            .OrderBy(x => x.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        foreach (var attachment in attachments)
        {
            var result = await extractor.ExtractDocxTextAsync(attachment.FilePath, ct);
            attachment.DocumentExtractionStatus = result.Succeeded ? "Succeeded" : "Failed";
            attachment.DocumentText = result.Succeeded ? result.Text : null;
            attachment.DocumentExtractedAt = DateTime.UtcNow;
            attachment.DocumentExtractionError = result.FailureReason;
            await db.SaveChangesAsync(ct);
        }
    }
}
