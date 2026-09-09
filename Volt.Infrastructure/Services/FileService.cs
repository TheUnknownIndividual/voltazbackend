using FluentFTP;
using Microsoft.Extensions.Options;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Infrastructure.Configuration;

namespace Volt.Infrastructure.Services
{
    public sealed class FileService : IFileService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".svg", ".webp"
        };

        private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx"
        };

        // Covers the WhatsApp attachment types the Meta inbox mirrors: voice
        // notes/audio, video, and common document formats.
        private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".ogg", ".oga", ".mp3", ".m4a", ".aac", ".amr", ".wav",
            ".mp4", ".3gp", ".3gpp", ".mov",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip"
        };

        private readonly FtpOptions _ftpOptions;

        public FileService(IOptions<FtpOptions> ftpOptions)
        {
            _ftpOptions = ftpOptions.Value;
        }

        public Task<string> UploadImageAsync(FileUploadRequest file, string folderName, CancellationToken ct = default)
            => UploadFileAsync(file, folderName, ImageExtensions, ct);

        public Task<string> UploadPdfAsync(FileUploadRequest file, string folderName, CancellationToken ct = default)
            => UploadFileAsync(file, folderName, PdfExtensions, ct);

        public Task<string> UploadMediaAsync(FileUploadRequest file, string folderName, CancellationToken ct = default)
            => UploadFileAsync(file, folderName, MediaExtensions, ct);

        public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                throw new ArgumentException("File url is required", nameof(fileUrl));
            }

            var baseUrl = _ftpOptions.BaseUrl.TrimEnd('/');

            if (!fileUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("File url does not belong to this storage.");
            }

            var relativePath = fileUrl.Substring(baseUrl.Length).TrimStart('/');
            var remoteFilePath = $"{_ftpOptions.BasePath.TrimEnd('/')}/{relativePath}";

            using var ftp = new AsyncFtpClient(
                _ftpOptions.Host,
                _ftpOptions.Username,
                _ftpOptions.Password,
                _ftpOptions.Port);

            await ftp.Connect(ct);

            var exists = await ftp.FileExists(remoteFilePath, ct);
            if (!exists)
            {
                throw new FileNotFoundException("File not found on FTP.", remoteFilePath);
            }

            await ftp.DeleteFile(remoteFilePath, ct);
            await ftp.Disconnect(ct);
        }

        private async Task<string> UploadFileAsync(
            FileUploadRequest file,
            string folderName,
            HashSet<string> allowedExtensions,
            CancellationToken ct)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Invalid file extension.");
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var safeFolderName = string.IsNullOrWhiteSpace(folderName)
                ? "common"
                : folderName.Trim().ToLowerInvariant();

            var remoteDirectory = $"{_ftpOptions.BasePath.TrimEnd('/')}/{safeFolderName}";
            var remoteFilePath = $"{remoteDirectory}/{fileName}";
            var relativePath = $"{safeFolderName}/{fileName}";

            if (file.Content.CanSeek)
            {
                file.Content.Position = 0;
            }

            using var ftp = new AsyncFtpClient(
                _ftpOptions.Host,
                _ftpOptions.Username,
                _ftpOptions.Password,
                _ftpOptions.Port);

            await ftp.Connect(ct);

            if (!await ftp.DirectoryExists(remoteDirectory, ct))
            {
                await ftp.CreateDirectory(remoteDirectory, ct);
            }

            await ftp.UploadStream(file.Content, remoteFilePath, token: ct);
            await ftp.Disconnect(ct);

            return $"{_ftpOptions.BaseUrl.TrimEnd('/')}/{relativePath}";
        }
    }
}
