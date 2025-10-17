// See https://aka.ms/new-console-template for more information

using AppImageManager;

if (!OperatingSystem.IsLinux())
{
    Console.WriteLine("AppImageManager only supports Linux");
    return;
}

var command = args.FirstOrDefault()?.Trim().ToLower();

if (command == "check-desktop-file" && args.Length >= 3)
{
    var appImagePath = args.Skip(1).FirstOrDefault();
    var appId = args.Skip(2).FirstOrDefault();

    if (string.IsNullOrEmpty(appId))
    {
        Console.WriteLine("Missing AppId");
        return;
    }
    
    var doesExist = AppImage.DoesDesktopFileExist(appId, appImagePath);
    Console.WriteLine($"Desktop file for {appId} " + (doesExist ? "exists" : "does not exist"));
}
else if (command == "download-app-image" && args.Length >= 3)
{
    var appImagePath = args.Skip(1).FirstOrDefault();
    var downloadUrl = args.Skip(2).FirstOrDefault();

    if (string.IsNullOrEmpty(appImagePath) || string.IsNullOrEmpty(downloadUrl))
    {
        Console.WriteLine("Missing app image path or download url");
        return;
    }
    
    var downloadResponse = await AppImage.Download(new DownloadAppImageRequest
    {
        Url = downloadUrl,
        AppImagePath = appImagePath,
        AutoLaunch = true
    });

    Console.WriteLine(downloadResponse.Success
        ? "Downloaded app image sucessfully"
        : $"Download of app image failed: {downloadResponse.ErrorMessage}");
}
else if (command == "create-desktop-file" && args.Length >= 4)
{
    var appImagePath = args.Skip(1).FirstOrDefault();
    var appId = args.Skip(2).FirstOrDefault();
    var appName = args.Skip(3).FirstOrDefault();
    
    if (string.IsNullOrEmpty(appImagePath) || string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appName))
    {
        Console.WriteLine("Missing appImagePath, appId, or appName");
        return;
    }
    
    var response = new DesktopFileBuilder(appId, appName)
        .ForAppImageFile(appImagePath)
        .AddUninstallAction()
        .Build();

    Console.WriteLine(response.Success
        ? "Desktop file created successfully"
        : $"Desktop file creation failed: {response.ErrorMessage}");
    
    Console.WriteLine("Press any key to delete files and exit");
    Console.ReadKey();

    foreach (var fileToDelete in (response.AddedFiles ?? []).Where(File.Exists))
    {
        File.Delete(fileToDelete);
    }
}
else
{
    Console.WriteLine("Invalid input");
    Console.WriteLine("Uses:");
    Console.WriteLine(" - AppImageManagerConsoleApp check-desktop-file <appImagePath> <appId>");
    Console.WriteLine(" - AppImageManagerConsoleApp download-app-image <appImagePath> <downloadUrl>");
    Console.WriteLine(" - AppImageManagerConsoleApp create-desktop-file <appImagePath> <appId> <appName>");
}