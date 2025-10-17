namespace AppImageManager;

internal class PathData
{
    public required string AppId { get; init; }
    public required string AppName { get; init; }
    public required string AppImagePath { get; init; }
    public required string AppImageFolder { get; init; }
    public required string MountFolder { get; init; }
    public required string DesktopFilePath { get; init; }
    public List<string>? IconPaths { get; set; }
    public string? UninstallFilePath { get; set; }
    public string? PrimaryIcon { get; set; }

    public string EscapedAppImagePath => GetEscapedPathForDesktop(AppImagePath);
    public string EscapedAppImageFolder => GetEscapedPathForDesktop(AppImageFolder);
    public string EscapedPrimaryIcon => GetEscapedPathForDesktop(PrimaryIcon);
    public string EscapedUninstallFilePath =>  GetEscapedPathForDesktop(UninstallFilePath);
    
    internal static string GetEscapedPathForDesktop(string? path)
    {
        return path?.Replace(" ", "\\s") ?? "";
    }
    
    internal string ApplyReplacements(string toUpdate)
    {
        toUpdate = toUpdate.Replace("%AppName%", AppName);
        toUpdate = toUpdate.Replace("%AppPath%", AppImagePath);
        toUpdate = toUpdate.Replace("%EscapedAppPath%", EscapedAppImagePath);
        toUpdate = toUpdate.Replace("%FolderPath%", AppImageFolder);
        toUpdate = toUpdate.Replace("%DesktopFilePath%", DesktopFilePath);
        toUpdate = toUpdate.Replace("%UninstallFilePath%", UninstallFilePath);
        return toUpdate;
    }
}