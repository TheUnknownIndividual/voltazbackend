using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos
{
    public class UploadImageRequest
    {
        public FileUploadRequest File { get; set; }
        public string FolderName { get; set; }
    }
}
