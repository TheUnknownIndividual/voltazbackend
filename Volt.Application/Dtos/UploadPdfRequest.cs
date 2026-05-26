namespace Volt.Application.Dtos
{
    public class UploadPdfRequest
    {
        public FileUploadRequest File { get; set; }
        public string FolderName { get; set; }
    }
}
