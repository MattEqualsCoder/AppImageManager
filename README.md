# App Image Manager

Nuget Package for helping manage AppImage files on Linux. It can create .desktop files for adding the AppImage to the user's desktop environment menu, and it can download and launch AppImages from urls. Can be used with [PupNet-Deploy](https://github.com/kuiperzone/PupNet-Deploy) for single file .net applications that will automatically add themselves to the menu and auto update. (Getting the download url is not part of this package.)

Most functions can either be provided a path to an AppImage file, or auto-detect the AppImage file and directory using the automatically created environment variables when launching AppImage files.

## Basic Usage

### Create desktop file if it does not exist
```csharp
if (!AppImage.DoesDesktopFileExist("org.mattequalscoder.example"))
{
    return new DesktopFileBuilder("org.mattequalscoder.example", "Example App")
        .AddUninstallAction(Directories.BaseFolder)
        .Build();
}
```

### Download app image and execute
```csharp
var downloadResponse = await AppImage.DownloadAsync(new DownloadAppImageRequest
{
    Url = "https://github.com/MattEqualsCoder/ExampleProject/releases/download/v1.0.0/ExampleProject.x86_64.AppImage",
    AutoLaunch = true
});

Console.WriteLine(downloadResponse.Success
    ? "Downloaded app image sucessfully"
    : $"Download of app image failed: {downloadResponse.ErrorMessage}");
```

## Directories

The following directories are used:

* `~/.local/share/applications` - This is where the .desktop file is created
* `~/.icons` - This is where svg icon files are placed
* `~/.icons/hicolor/<imagesize>/apps` - This is where sized png files are placed
* `~/.local/share/app-image-uninstalls` - If the uninstall action is requested, this is where the bash file is created