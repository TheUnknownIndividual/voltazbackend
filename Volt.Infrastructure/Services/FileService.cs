using FluentFTP;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Infrastructure.Configuration;

namespace Volt.Infrastructure.Services
{
    public sealed class FileService : IFileService
    {
        private readonly FtpOptions _ftpOptions;

        public FileService(IOptions<FtpOptions> ftpOptions)
        {
            _ftpOptions = ftpOptions.Value;
        }

        public async Task<string> UploadImageAsync(FileUploadRequest file, string folderName, CancellationToken ct = default)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
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

        public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new ArgumentException("File url is required", nameof(fileUrl));

            var baseUrl = _ftpOptions.BaseUrl.TrimEnd('/');

            if (!fileUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("File url does not belong to this storage.");

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
                throw new FileNotFoundException("File not found on FTP.", remoteFilePath);

            await ftp.DeleteFile(remoteFilePath, ct);
            await ftp.Disconnect(ct);
        }
    }
}
