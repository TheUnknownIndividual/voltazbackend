using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;

namespace Volt.Application.Interfaces
{
    public interface IFileService
    {
        Task<string> UploadImageAsync(FileUploadRequest file, string folderName, CancellationToken ct = default);
        Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
    }
}
