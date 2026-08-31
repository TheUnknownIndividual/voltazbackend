using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Product;
using Volt.Application.Interfaces;

namespace Volt.API.Services
{
    public sealed class ProductDatasheetUploadService
    {
        private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

        private readonly IFileService _files;
        private readonly ProductAiImportOptions _options;

        public ProductDatasheetUploadService(IFileService files, IOptions<ProductAiImportOptions> options)
        {
            _files = files;
            _options = options.Value;
        }

        public async Task<IReadOnlyList<ProductDatasheetUploadDto>> UploadAsync(
            IReadOnlyList<IFormFile> files,
            CancellationToken ct)
        {
            if (files.Count is < 1 || files.Count > 10 || files.Count > _options.MaxFiles)
                throw new InvalidDataException($"1-{Math.Min(10, _options.MaxFiles)} datasheet files are required.");

            var combinedBytes = files.Sum(x => x.Length);
            if (combinedBytes <= 0 || combinedBytes > Math.Min(50L * 1024 * 1024, _options.MaxCombinedBytes))
                throw new InvalidDataException("Datasheet files must have a combined size of 50 MB or less.");

            var validated = new List<(IFormFile File, string Extension, string MimeType)>();
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedTypes.TryGetValue(extension, out var expectedMime) || file.Length <= 0)
                    throw new InvalidDataException("Only PDF, JPEG, PNG, and WebP datasheets are supported.");

                await using var signatureStream = file.OpenReadStream();
                var header = new byte[Math.Min(16, (int)file.Length)];
                var read = await signatureStream.ReadAsync(header.AsMemory(), ct);
                if (!MatchesSignature(extension, header.AsSpan(0, read)))
                    throw new InvalidDataException($"The file '{Path.GetFileName(file.FileName)}' does not match its extension.");

                validated.Add((file, extension, expectedMime));
            }

            if (validated.Any(x => x.Extension == ".pdf") && validated.Any(x => x.Extension != ".pdf"))
                throw new InvalidDataException("Upload either PDF datasheets or image datasheets, not both together.");

            var uploaded = new List<ProductDatasheetUploadDto>();
            try
            {
                foreach (var item in validated)
                {
                    await using var stream = item.File.OpenReadStream();
                    var request = new FileUploadRequest
                    {
                        FileName = Path.GetFileName(item.File.FileName),
                        Content = stream
                    };
                    var url = item.Extension == ".pdf"
                        ? await _files.UploadPdfAsync(request, "product-datasheets", ct)
                        : await _files.UploadImageAsync(request, "product-datasheets", ct);
                    uploaded.Add(new ProductDatasheetUploadDto(
                        url,
                        item.MimeType,
                        item.File.Length,
                        Path.GetFileName(item.File.FileName)));
                }
                return uploaded;
            }
            catch
            {
                foreach (var item in uploaded)
                {
                    try { await _files.DeleteFileAsync(item.Url, CancellationToken.None); }
                    catch { }
                }
                throw;
            }
        }

        private static bool MatchesSignature(string extension, ReadOnlySpan<byte> header)
            => extension switch
            {
                ".pdf" => header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8),
                ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
                _ => false
            };
    }
}
