using System.IO.Compression;
using System.Text;
using System.Xml;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services;

/// <summary>
/// Extracts text only from Volt's own cloud-hosted DOCX attachments. The limits
/// intentionally keep a malformed or very large document from consuming API RAM.
/// </summary>
public sealed class ProjectAttachmentDocumentExtractor : IProjectAttachmentDocumentExtractor
{
    private const int MaxDownloadBytes = 8 * 1024 * 1024;
    private const int MaxDocumentXmlBytes = 5 * 1024 * 1024;
    private const int MaxExtractedCharacters = 200_000;
    private readonly HttpClient _httpClient;

    public ProjectAttachmentDocumentExtractor(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<ProjectAttachmentDocumentExtractionResult> ExtractDocxTextAsync(string fileUrl, CancellationToken ct = default)
    {
        if (!IsTrustedDocxUrl(fileUrl)) return new(false, string.Empty, "Only Volt cloud DOCX attachments can be extracted.");

        try
        {
            using var response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return new(false, string.Empty, $"Document download returned HTTP {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength is long length && length > MaxDownloadBytes) return new(false, string.Empty, "Document exceeds the 8 MB extraction limit.");

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var buffer = new MemoryStream();
            var bytes = new byte[80 * 1024];
            var total = 0;
            while (true)
            {
                var read = await source.ReadAsync(bytes, ct);
                if (read == 0) break;
                total += read;
                if (total > MaxDownloadBytes) return new(false, string.Empty, "Document exceeds the 8 MB extraction limit.");
                await buffer.WriteAsync(bytes.AsMemory(0, read), ct);
            }

            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);
            var documentXml = archive.GetEntry("word/document.xml");
            if (documentXml is null) return new(false, string.Empty, "The DOCX main document XML is missing.");
            if (documentXml.Length > MaxDocumentXmlBytes) return new(false, string.Empty, "Document text exceeds the extraction limit.");

            await using var xmlStream = documentXml.Open();
            using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxExtractedCharacters * 4L,
                IgnoreWhitespace = true
            });

            var text = new StringBuilder();
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Text)
                {
                    text.Append(reader.Value);
                }
                else if (reader.NodeType == XmlNodeType.EndElement && (reader.LocalName == "p" || reader.LocalName == "tr"))
                {
                    text.AppendLine();
                }
                if (text.Length >= MaxExtractedCharacters) break;
            }

            var normalized = string.Join('\n', text.ToString().Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
            return new(true, normalized[..Math.Min(normalized.Length, MaxExtractedCharacters)], null);
        }
        catch (InvalidDataException)
        {
            return new(false, string.Empty, "The attachment is not a readable DOCX file.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, string.Empty, "Document extraction timed out.");
        }
        catch
        {
            return new(false, string.Empty, "Document extraction failed.");
        }
    }

    private static bool IsTrustedDocxUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && string.Equals(uri.Host, "cloudfiles.volt.az", StringComparison.OrdinalIgnoreCase)
           && uri.AbsolutePath.StartsWith("/documents/", StringComparison.Ordinal)
           && uri.AbsolutePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);
}
