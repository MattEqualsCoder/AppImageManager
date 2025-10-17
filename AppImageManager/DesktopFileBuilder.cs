using System.Diagnostics;
using System.Runtime.Versioning;

namespace AppImageManager;

/// <summary>
/// Build implementation of AppImageManager to generate a .desktop
/// file for an AppImage file
/// </summary>
/// <param name="appId">A unique reverse DNS identifier for the app</param>
/// <param name="appName">A friendly display name for the app</param>
[SupportedOSPlatform("linux")]
public class DesktopFileBuilder(string appId, string appName)
{
    private readonly List<CustomAction> _customActions = [];
    private bool _addUninstallAction;
    private readonly List<string> _additionalUninstallPaths = [];
    private CustomMimeTypeInfo? _mimeTypeInfo;
    private string? _appImagePath;

    /// <summary>
    /// Add an action to the desktop for quick list functionality to perform a specific action.
    /// </summary>
    /// <param name="code">Unique code for the quick list entry</param>
    /// <param name="name">The display name of the quick list entry</param>
    /// <param name="arguments">The command line arguments to pass to the application.</param>
    /// <param name="icon">The icon to display</param>
    public DesktopFileBuilder AddCustomArgumentsAction(string code, string name, string arguments, string? icon = null)
    {
        _customActions.Add(new CustomAction
        {
            Code = code,
            Name = name,
            Command = $"\"%EscapedAppPath%\" {arguments}",
            Icon = icon
        });
        return this;
    }
    
    /// <summary>
    /// Add an action to the desktop for quick list functionality to perform a specific action.
    /// </summary>
    /// <param name="code">Unique code for the quick list entry</param>
    /// <param name="name">The display name of the quick list entry</param>
    /// <param name="command">The full path and arguments to use for the action</param>
    /// <param name="icon">The icon to display</param>\
    public DesktopFileBuilder AddCustomCommandAction(string code, string name, string command, string? icon = null)
    {
        _customActions.Add(new CustomAction
        {
            Code = code,
            Name = name,
            Command = command,
            Icon = icon
        });
        return this;
    }

    /// <summary>
    /// Adds an uninstall bash script and action to the jump list to remove the AppImage, Desktop, and Icon files
    /// </summary>
    public DesktopFileBuilder AddUninstallAction()
    {
        _addUninstallAction = true;
        return this;
    }

    /// <summary>
    /// Adds an uninstall bash script and action to the jump list to remove the AppImage, Desktop, and Icon files
    /// </summary>
    /// <param name="additionalUninstallPaths">Additional files or folders to remove when uninstalling</param>
    public DesktopFileBuilder AddUninstallAction(params string[] additionalUninstallPaths)
    {
        _addUninstallAction = true;
        _additionalUninstallPaths.AddRange(additionalUninstallPaths);
        return this;
    }

    public DesktopFileBuilder WithMimeType(string mimeType, string description, string globPattern, bool autoAssociate)
    {
        _mimeTypeInfo = new CustomMimeTypeInfo
        {
            MimeType = mimeType,
            Description = description,
            GlobPattern = globPattern,
            AutoAssociate = autoAssociate
        };
        return this;
    }

    public DesktopFileBuilder ForAppImageFile(string? appImagePath)
    {
        if (string.IsNullOrEmpty(appImagePath) || !File.Exists(appImagePath))
        {
            return this;
        }
        
        _appImagePath = appImagePath;
        return this;
    }
    
    /// <summary>
    /// Build the desktop file
    /// </summary>
    /// <returns>The response for if it was successful or not</returns>
    public CreateDesktopFileResponse Build()
    {
        return AppImage.CreateDesktopFile(new CreateDesktopFileRequest
        {
            AppId = appId,
            AppName = appName,
            CustomActions = _customActions,
            AddUninstallAction = _addUninstallAction,
            AdditionalUninstallPaths = _additionalUninstallPaths,
            CustomMimeTypeInfo = _mimeTypeInfo,
            AppImageFilePath = _appImagePath,
        });
    }
}