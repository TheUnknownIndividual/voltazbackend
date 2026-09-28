using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;

namespace Volt.API.Services
{
    // Renders the branded "question card" image for educational posts: the hook question in large text
    // over a Volt-green gradient with a solar-panel grid. 1080x1350 (4:5) JPEG, valid for Instagram.
    // System.Drawing is Windows-only on .NET 8 (the API runs on IIS/Windows); elsewhere this returns null.
    public sealed class SocialCardService
    {
        private const int Width = 1080;
        private const int Height = 1350;

        private readonly IFileService _fileService;
        private readonly ILogger<SocialCardService> _logger;

        public SocialCardService(IFileService fileService, ILogger<SocialCardService> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        // Returns the public CDN URL of the rendered card, or null if it could not be produced.
        public async Task<string?> RenderAndUploadAsync(string hook, CancellationToken ct)
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(hook)) return null;
            try
            {
                var bytes = Render(hook.Trim());
                using var stream = new MemoryStream(bytes);
                return await _fileService.UploadImageAsync(
                    new FileUploadRequest { FileName = "question-card.jpg", Content = stream }, "social", ct);
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
            {
                _logger.LogWarning("Question card could not be rendered ({Error})", ex.GetType().Name);
                return null;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static byte[] Render(string hook)
        {
            var dark = ColorTranslator.FromHtml("#0b2b23");
            var primary = ColorTranslator.FromHtml("#2b8659");
            var accent = ColorTranslator.FromHtml("#7fe0a7");

            using var bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using (var background = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), dark, primary, 60f))
                g.FillRectangle(background, 0, 0, Width, Height);

            // Solar-panel grid: faint cell lines across the lower two thirds, slightly skewed like a panel.
            using (var gridPen = new Pen(Color.FromArgb(38, 255, 255, 255), 2f))
            {
                const int cell = 90;
                for (var x = 0; x <= Width; x += cell) g.DrawLine(gridPen, x, 520, x - 120, Height);
                for (var y = 520; y <= Height; y += cell) g.DrawLine(gridPen, 0, y, Width, y);
            }
            using (var glow = new SolidBrush(Color.FromArgb(60, accent)))
                g.FillEllipse(glow, Width - 360, -220, 620, 620);   // "sun"

            var fontName = FontFamily.Families.Any(f => f.Name == "Segoe UI") ? "Segoe UI" : "Arial";
            var padding = 90;
            var textBox = new RectangleF(padding, 250, Width - 2 * padding, 700);

            // Shrink the font until the whole question fits its box.
            var size = 84f;
            Font? font = null;
            using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            while (size >= 40f)
            {
                font?.Dispose();
                font = new Font(fontName, size, FontStyle.Bold, GraphicsUnit.Pixel);
                var measured = g.MeasureString(hook, font, (int)textBox.Width, format);
                if (measured.Height <= textBox.Height) break;
                size -= 4f;
            }

            using (var labelFont = new Font(fontName, 34f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var labelBrush = new SolidBrush(accent))
                g.DrawString("GÜNƏŞ ENERJİSİ HAQQINDA", labelFont, labelBrush, padding, 150);

            using (var textBrush = new SolidBrush(Color.White))
                g.DrawString(hook, font!, textBrush, textBox, format);
            font?.Dispose();

            using (var footerFont = new Font(fontName, 38f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var footerBrush = new SolidBrush(Color.White))
            using (var hintBrush = new SolidBrush(accent))
            {
                g.DrawString("Volt.az", footerFont, footerBrush, padding, Height - 150);
                var hint = "Cavab yazıdadır  →";
                var hintSize = g.MeasureString(hint, footerFont);
                g.DrawString(hint, footerFont, hintBrush, Width - padding - hintSize.Width, Height - 150);
            }

            var encoder = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
            using var output = new MemoryStream();
            bitmap.Save(output, encoder, parameters);
            return output.ToArray();
        }
    }
}
