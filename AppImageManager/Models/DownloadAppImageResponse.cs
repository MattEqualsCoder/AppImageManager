namespace AppImageManager;

public class DownloadAppImageResponse
{
    public bool Success { get; set; }
    public bool DownloadedSuccessfully { get; set; }
    public bool LaunchedSuccessfully { get; set; }
    public string? ErrorMessage { get; set; }
}