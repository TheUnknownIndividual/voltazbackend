using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services;

public sealed class SolarInverterDatasheetParser : ISolarInverterDatasheetParser
{
    private sealed record PageText(int PageNumber, string Text, string ExtractionMethod);

    private const string ParserVersion = "volt-pdfpig-tesseract-1.2";
    private static readonly Regex EngineeringLabel = new(
        @"(?ix)
        (?:model|type|revision|version|rated|nominal|max(?:imum)?|min(?:imum)?|voltage|
        current|power|frequency|mppt|mpp|input|output|efficiency|protection|ip\s*\d{2}|
        operating|temperature|humidity|altitude|dimension|weight|cooling|noise|
        communication|interface|warranty|certificat|standard|grid|islanding|thd|
        power\s*factor|spd|afci|switch|string|short[\s-]*circuit)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));
    private readonly SolarInverterOcrOptions _ocrOptions;

    public SolarInverterDatasheetParser(IOptions<SolarInverterOcrOptions> ocrOptions)
    {
        _ocrOptions = ocrOptions.Value;
    }

    public async Task<ParsedSolarInverterDatasheet> ParseAsync(
        byte[] pdfContent,
        string sourceUrl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfContent);
        if (pdfContent.Length == 0)
        {
            throw new InvalidDataException("The linked datasheet asset is empty.");
        }

        if (!IsPdf(pdfContent))
        {
            return await ParseImageAsync(pdfContent, sourceUrl, ct);
        }

        var warnings = new List<string>();
        var pageTexts = ExtractEmbeddedText(pdfContent);
        var pageCount = pageTexts.Count;
        var minimumCharacters = Math.Max(
            100,
            pageCount * Math.Clamp(_ocrOptions.MinimumEmbeddedCharactersPerPage, 20, 200));
        var requiresOcr = pageTexts.Sum(page => page.Text.Length) < minimumCharacters;
        var ocrAttempted = false;
        var ocrSucceeded = false;

        if (requiresOcr && _ocrOptions.Enabled)
        {
            ocrAttempted = true;
            try
            {
                var ocrTexts = await ExtractOcrTextAsync(pdfContent, pageCount, ct);
                pageTexts = pageTexts
                    .Select(page =>
                    {
                        if (page.Text.Length >= _ocrOptions.MinimumEmbeddedCharactersPerPage ||
                            !ocrTexts.TryGetValue(page.PageNumber, out var ocrText) ||
                            string.IsNullOrWhiteSpace(ocrText))
                        {
                            return page;
                        }

                        return new PageText(page.PageNumber, ocrText.Trim(), "tesseract-ocr");
                    })
                    .ToList();
                ocrSucceeded = pageTexts.Any(page => page.ExtractionMethod == "tesseract-ocr");
                requiresOcr = pageTexts.Sum(page => page.Text.Length) < minimumCharacters;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                warnings.Add($"OCR could not be completed: {error.Message}");
            }
        }

        if (requiresOcr)
        {
            warnings.Add(
                _ocrOptions.Enabled
                    ? "Very little readable text remains after extraction. Visual review is required."
                    : "Very little embedded text was found and OCR is disabled.");
        }

        var extractedTextBuilder = new StringBuilder();
        var engineeringLines = new List<object>();
        foreach (var page in pageTexts)
        {
            extractedTextBuilder.AppendLine($"--- Page {page.PageNumber} ({page.ExtractionMethod}) ---");
            extractedTextBuilder.AppendLine(page.Text);
            foreach (var line in SplitLines(page.Text))
            {
                if (line.Length is < 3 or > 500 || !EngineeringLabel.IsMatch(line))
                {
                    continue;
                }

                engineeringLines.Add(new
                {
                    page = page.PageNumber,
                    text = line
                });
            }
        }

        var extractedText = extractedTextBuilder.ToString().Trim();
        var documentKind = ResolveDocumentKind(extractedText);
        if (documentKind != "manufacturer-datasheet")
        {
            warnings.Add(
                "The linked datasheet PDF does not appear to be a manufacturer datasheet; " +
                $"it was classified as {documentKind}.");
        }

        var parsedContentJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            parserVersion = ParserVersion,
            sourceUrl,
            documentKind,
            pageCount,
            requiresOcr,
            ocrAttempted,
            ocrSucceeded,
            pages = pageTexts.Select(page => new
            {
                page = page.PageNumber,
                characterCount = page.Text.Length,
                extractionMethod = page.ExtractionMethod
            }),
            engineeringLines,
            warnings
        });

        return new ParsedSolarInverterDatasheet(
            pageCount,
            requiresOcr,
            ocrAttempted,
            ocrSucceeded,
            extractedText,
            parsedContentJson,
            documentKind,
            warnings);
    }

    private async Task<ParsedSolarInverterDatasheet> ParseImageAsync(
        byte[] imageContent,
        string sourceUrl,
        CancellationToken ct)
    {
        if (!_ocrOptions.Enabled)
        {
            throw new InvalidOperationException(
                "OCR must be enabled to extract displayed datasheet images.");
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"volt-inverter-image-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var extension = ResolveImageExtension(sourceUrl);
        var inputPath = Path.Combine(temporaryDirectory, $"datasheet{extension}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_ocrOptions.TimeoutSeconds, 30, 900)));

        try
        {
            await File.WriteAllBytesAsync(inputPath, imageContent, timeout.Token);
            var extractedText = (await RunProcessAsync(
                _ocrOptions.TesseractExecutable,
                [
                    inputPath,
                    "stdout",
                    "-l", string.IsNullOrWhiteSpace(_ocrOptions.Languages)
                        ? "eng"
                        : _ocrOptions.Languages,
                    "--psm", "6"
                ],
                timeout.Token)).Trim();
            var requiresOcr = extractedText.Length < 100;
            var warnings = new List<string>();
            if (requiresOcr)
            {
                warnings.Add(
                    "Very little readable text was extracted from the displayed datasheet image.");
            }

            var detectedKind = ResolveDocumentKind(extractedText);
            var documentKind = detectedKind == "certificate"
                ? detectedKind
                : "manufacturer-datasheet-image";
            if (documentKind == "certificate")
            {
                warnings.Add(
                    "The displayed asset was detected as a certificate and must not enter datasheet QA.");
            }

            var engineeringLines = SplitLines(extractedText)
                .Where(line => line.Length is >= 3 and <= 500 && EngineeringLabel.IsMatch(line))
                .Select(line => new { page = 1, text = line })
                .ToList();
            var parsedContentJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 3,
                parserVersion = ParserVersion,
                sourceUrl,
                documentKind,
                pageCount = 1,
                requiresOcr,
                ocrAttempted = true,
                ocrSucceeded = !requiresOcr,
                pages = new[]
                {
                    new
                    {
                        page = 1,
                        characterCount = extractedText.Length,
                        extractionMethod = "tesseract-image-ocr"
                    }
                },
                engineeringLines,
                warnings
            });

            return new ParsedSolarInverterDatasheet(
                1,
                requiresOcr,
                true,
                !requiresOcr,
                $"--- Image 1 (tesseract-image-ocr) ---{Environment.NewLine}{extractedText}".Trim(),
                parsedContentJson,
                documentKind,
                warnings);
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
                // The OS temp cleaner can safely remove a file still held by a failed native process.
            }
        }
    }

    private static bool IsPdf(byte[] content)
        => content.Length >= 5
            && Encoding.ASCII.GetString(content, 0, 5)
                .Equals("%PDF-", StringComparison.Ordinal);

    private static string ResolveImageExtension(string sourceUrl)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (extension is ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp" or ".webp")
            {
                return extension;
            }
        }

        return ".png";
    }

    private static List<PageText> ExtractEmbeddedText(byte[] pdfContent)
    {
        var pages = new List<PageText>();
        using var document = PdfDocument.Open(pdfContent);
        foreach (var page in document.GetPages())
        {
            pages.Add(new PageText(
                page.Number,
                ContentOrderTextExtractor.GetText(page) ?? string.Empty,
                "embedded-text"));
        }

        return pages;
    }

    private async Task<IReadOnlyDictionary<int, string>> ExtractOcrTextAsync(
        byte[] pdfContent,
        int pageCount,
        CancellationToken ct)
    {
        var maximumPages = Math.Clamp(_ocrOptions.MaxPages, 1, 100);
        if (pageCount > maximumPages)
        {
            throw new InvalidDataException(
                $"The PDF has {pageCount} pages; OCR is limited to {maximumPages} pages per document.");
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"volt-inverter-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var inputPath = Path.Combine(temporaryDirectory, "input.pdf");
        var result = new Dictionary<int, string>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_ocrOptions.TimeoutSeconds, 30, 900)));

        try
        {
            await File.WriteAllBytesAsync(inputPath, pdfContent, timeout.Token);
            for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var outputPrefix = Path.Combine(temporaryDirectory, $"page-{pageNumber}");
                await RunProcessAsync(
                    _ocrOptions.PdfToPpmExecutable,
                    [
                        "-f", pageNumber.ToString(),
                        "-l", pageNumber.ToString(),
                        "-r", Math.Clamp(_ocrOptions.Dpi, 120, 300).ToString(),
                        "-png",
                        "-singlefile",
                        inputPath,
                        outputPrefix
                    ],
                    timeout.Token);

                var imagePath = $"{outputPrefix}.png";
                var ocrText = await RunProcessAsync(
                    _ocrOptions.TesseractExecutable,
                    [
                        imagePath,
                        "stdout",
                        "-l", string.IsNullOrWhiteSpace(_ocrOptions.Languages)
                            ? "eng"
                            : _ocrOptions.Languages,
                        "--psm", "6"
                    ],
                    timeout.Token);
                result[pageNumber] = ocrText;
                File.Delete(imagePath);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
                // The OS temp cleaner can safely remove a file still held by a failed native process.
            }
        }

        return result;
    }

    private static async Task<string> RunProcessAsync(
        string executable,
        IReadOnlyCollection<string> arguments,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start {executable}.");
            }
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                $"Required OCR executable '{executable}' is unavailable.",
                error);
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(ct);
        var standardError = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = await standardOutput;
        var errorOutput = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{executable} exited with code {process.ExitCode}: {errorOutput.Trim()}");
        }

        return output;
    }

    private static IEnumerable<string> SplitLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim());

    private static string ResolveDocumentKind(string text)
    {
        if (text.Contains("certificate of conformity", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("certification body", StringComparison.OrdinalIgnoreCase))
        {
            return "certificate";
        }

        if (text.Contains("datasheet", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("technical data", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("technical specifications", StringComparison.OrdinalIgnoreCase))
        {
            return "manufacturer-datasheet";
        }

        if (text.Contains("installation manual", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("user manual", StringComparison.OrdinalIgnoreCase))
        {
            return "manual";
        }

        return "technical-document";
    }
}
