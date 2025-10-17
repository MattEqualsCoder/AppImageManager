namespace AppImageManager;

public class DownloadAppImageRequest
{
    public required string Url { get; set; }
    public string? AppImagePath { get; set; }
    public int DownloadAttempts { get; set; } = 3;
    public bool AutoLaunch { get; set; } = true;
}