using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Infrastructure.Configuration;

namespace Volt.API.Services
{
    // Instagram only accepts a public JPEG whose aspect ratio is between 4:5 and 1.91:1.
    // Images already in range and already JPEG are used untouched; anything else is re-encoded
    // and center-cropped into range, then hosted on the Volt file storage.
    public sealed class SocialImageService
    {
        private const double MinRatio = 0.8;   // 4:5
        private const double MaxRatio = 1.91;
        private const int MaxSide = 1440;

        private readonly HttpClient _client;
        private readonly IFileService _fileService;
        private readonly SocialPostingOptions _options;
        private readonly string? _cdnHost;
        private readonly ILogger<SocialImageService> _logger;

        public SocialImageService(
            HttpClient client,
            IFileService fileService,
            IOptions<SocialPostingOptions> options,
            IOptions<FtpOptions> ftpOptions,
            ILogger<SocialImageService> logger)
        {
            _client = client;
            _fileService = fileService;
            _options = options.Value;
            _logger = logger;
            _cdnHost = Uri.TryCreate(ftpOptions.Value.BaseUrl, UriKind.Absolute, out var cdn) ? cdn.Host : null;
        }

        // Returns a public https JPEG URL that satisfies Instagram, or null when the image is unusable.
        public async Task<string?> PrepareAsync(string? imageUrl, CancellationToken ct)
        {
            // Only fetch images we host ourselves; never an arbitrary URL.
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
            if (_cdnHost is null || !string.Equals(uri.Host, _cdnHost, StringComparison.OrdinalIgnoreCase)) return null;

            var isJpeg = uri.AbsolutePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || uri.AbsolutePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

            byte[] bytes;
            try
            {
                using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) return null;
                if (response.Content.Headers.ContentLength is > 0 and var length && length > _options.MaxImageBytes) return null;
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct)) > 0)
                {
                    if (buffer.Length + read > _options.MaxImageBytes) return null;
                    buffer.Write(chunk, 0, read);
                }
                bytes = buffer.ToArray();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogInformation("Social image download failed ({Error})", ex.GetType().Name);
                return null;
            }

            // System.Drawing is Windows-only on .NET 8 (the API runs on IIS/Windows). Elsewhere only
            // ready-to-use JPEGs can be passed through, and their ratio can't be verified.
            if (!OperatingSystem.IsWindows()) return isJpeg ? uri.ToString() : null;

            try
            {
                var processed = Process(bytes, isJpeg);
                if (processed is null) return uri.ToString();   // already valid, use the original
                using var upload = new MemoryStream(processed);
                return await _fileService.UploadImageAsync(
                    new FileUploadRequest { FileName = "social.jpg", Content = upload }, "social", ct);
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
            {
                _logger.LogInformation("Social image could not be processed ({Error})", ex.GetType().Name);
                return null;
            }
        }

        // Returns null when the original can be used as-is, otherwise the new JPEG bytes.
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static byte[]? Process(byte[] bytes, bool alreadyJpeg)
        {
            using var input = new MemoryStream(bytes);
            using var source = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: true);
            var ratio = (double)source.Width / source.Height;
            var inRange = ratio >= MinRatio && ratio <= MaxRatio;
            var isRealJpeg = alreadyJpeg && source.RawFormat.Equals(ImageFormat.Jpeg);
            if (inRange && isRealJpeg && source.Width <= MaxSide * 2) return null;

            // Center-crop into the allowed ratio range with the least possible cropping.
            var cropWidth = source.Width;
            var cropHeight = source.Height;
            if (ratio > MaxRatio) cropWidth = (int)Math.Floor(source.Height * MaxRatio);
            else if (ratio < MinRatio) cropHeight = (int)Math.Floor(source.Width / MinRatio);
            var cropX = (source.Width - cropWidth) / 2;
            var cropY = (source.Height - cropHeight) / 2;

            var scale = Math.Min(1d, (double)MaxSide / Math.Max(cropWidth, cropHeight));
            var targetWidth = Math.Max(1, (int)Math.Round(cropWidth * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(cropHeight * scale));

            using var target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(target))
            {
                graphics.Clear(Color.White);   // flatten transparency (PNG/WebP) onto white
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(
                    source,
                    new Rectangle(0, 0, targetWidth, targetHeight),
                    new Rectangle(cropX, cropY, cropWidth, cropHeight),
                    GraphicsUnit.Pixel);
            }

            var encoder = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
            using var output = new MemoryStream();
            target.Save(output, encoder, parameters);
            return output.ToArray();
        }
    }
}
