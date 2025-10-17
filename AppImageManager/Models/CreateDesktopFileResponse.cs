namespace AppImageManager;

public class CreateDesktopFileResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool MimeTypeSuccessful { get; set; }
    public string? MimeTypeError { get; set; }
    public List<string>? AddedFiles { get; set; }
}