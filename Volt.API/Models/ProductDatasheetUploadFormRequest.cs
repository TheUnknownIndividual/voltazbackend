using Microsoft.AspNetCore.Http;

namespace Volt.API.Models
{
    public sealed class ProductDatasheetUploadFormRequest
    {
        public List<IFormFile> Files { get; set; } = new();
    }
}
