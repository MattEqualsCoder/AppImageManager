using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using IniParser;
using IniParser.Model;
using IniParser.Model.Formatting;

namespace AppImageManager;

[SupportedOSPlatform("linux")]
public static class AppImage
{
    #region Public Methods
    public static bool IsRunningFromAppImage()
    {
        var appImageFilePath = Environment.GetEnvironmentVariable("APPIMAGE");
        return !string.IsNullOrEmpty(appImageFilePath) && File.Exists(appImageFilePath);
    }

    public static async Task<DownloadAppImageResponse> DownloadAsync(DownloadAppImageRequest request)
    {
        var appImageFilePath = request.AppImagePath;
        if (string.IsNullOrEmpty(appImageFilePath))
        {
            if (!IsRunningFromAppImage())
            {
                return new DownloadAppImageResponse
                {
                    Success = false,
                    ErrorMessage = "You must be running from an AppImage or specify an AppImage path"
                };
            }
            
            appImageFilePath = Environment.GetEnvironmentVariable("APPIMAGE")!;
        }
        
        if (!Directory.Exists(Path.GetTempPath()))
        {
            return new DownloadAppImageResponse()
            {
                Success = false,
                ErrorMessage = "Could not find location to download to"
            };
        }

        var tempDownloadPath = string.Empty;
        for (var i = 0; i < 3; i++)
        {
            tempDownloadPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!File.Exists(tempDownloadPath))
            {
                break;
            }
            else
            {
                tempDownloadPath = string.Empty;
            }
        }
        
        if (string.IsNullOrEmpty(tempDownloadPath))
        {
            return new DownloadAppImageResponse
            {
                Success = false,
                ErrorMessage = "Could not find location to download to"
            };
        }

        var downloadResponse = await Helper.DownloadFileAsyncAttempt(request.Url, tempDownloadPath, totalAttempts: request.DownloadAttempts);

        if (!downloadResponse.Item1)
        {
            return new DownloadAppImageResponse
            {
                Success = false,
                ErrorMessage = downloadResponse.Item2
            };
        }

        try
        {
            File.SetUnixFileMode(tempDownloadPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
        }
        catch (Exception e)
        {
            return new DownloadAppImageResponse
            {
                Success = false,
                ErrorMessage = $"Could not make AppImage executable: {e.Message}"
            };
        }

        var oldAppImagePath = Path.Combine(tempDownloadPath + ".old");
        
        try
        {
            if (File.Exists(appImageFilePath))
            {
                File.Move(appImageFilePath, oldAppImagePath);
            }
            File.Move(tempDownloadPath, appImageFilePath);
        }
        catch (Exception e)
        {
            return new DownloadAppImageResponse
            {
                Success = false,
                ErrorMessage = $"Could not move AppImage file: {e.Message}"
            };
        }

        if (File.Exists(oldAppImagePath))
        {
            try
            {
                File.Delete(oldAppImagePath);
            }
            catch
            {
                // Do nothing. In tmp directory, so should delete anyway
            }
        }

        if (request.AutoLaunch)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/setsid",
                    Arguments = $"\"{appImageFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch (Exception e)
            {
                return new DownloadAppImageResponse
                {
                    Success = false,
                    DownloadedSuccessfully = true,
                    ErrorMessage = $"Successfully downloaded, but could not launch AppImage: {e.Message}"
                };
            }
        }

        return new DownloadAppImageResponse
        {
            Success = true,
            DownloadedSuccessfully = true,
            LaunchedSuccessfully = request.AutoLaunch,
        };
    }
    
    public static bool DoesDesktopFileExist(string appId, string? appImageFilePath = null)
    {
        appImageFilePath ??= Environment.GetEnvironmentVariable("APPIMAGE");
        if (string.IsNullOrEmpty(appImageFilePath) || !File.Exists(appImageFilePath))
        {
            return true;
        }

        var desktopFilePath = GetDesktopFileName(GetDesktopFolder(), appId);
        if (!File.Exists(desktopFilePath))
        {
            return false;
        }
        
        var desktopFileContents = File.ReadAllText(desktopFilePath);
        return desktopFileContents.Contains(PathData.GetEscapedPathForDesktop(appImageFilePath));
    }

    public static CreateDesktopFileResponse CreateDesktopFile(string appId, string appName, string? appImageFileName = null)
    {
        return CreateDesktopFile(new CreateDesktopFileRequest()
        {
            AppId = appId,
            AppName = appName,
            AppImageFilePath = appImageFileName
        });
    }
    
    public static CreateDesktopFileResponse CreateDesktopFile(CreateDesktopFileRequest request)
    {
        var appImageFilePath = request.AppImageFilePath;

        if (string.IsNullOrEmpty(appImageFilePath) || !File.Exists(appImageFilePath))
        {
            appImageFilePath = Environment.GetEnvironmentVariable("APPIMAGE");
            if (string.IsNullOrEmpty(appImageFilePath) || !File.Exists(appImageFilePath))
            {
                return new CreateDesktopFileResponse
                {
                    Success = false,
                    ErrorMessage = "APPIMAGE missing from environment or the file is not found"
                };
            }
        }
        
        var mountPath = Environment.GetEnvironmentVariable("APPDIR");
        var deleteMountPath = false;
        if (string.IsNullOrEmpty(mountPath) || !Directory.Exists(mountPath))
        {
            mountPath = ExtractAppImage(appImageFilePath);
            if (!Directory.Exists(mountPath))
            {
                return new CreateDesktopFileResponse
                {
                    Success = false,
                    ErrorMessage = "APPDIR missing from environment or the directory is not found"
                };
            }
            else
            {
                deleteMountPath = true;
            }
        }

        var desktopFolderPath = GetDesktopFolder();

        try
        {
            if (!Directory.Exists(desktopFolderPath))
            {
                Directory.CreateDirectory(desktopFolderPath);
            }
        }
        catch (Exception e)
        {
            if (deleteMountPath)
            {
                Directory.Delete(mountPath, true);
            }
            
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the folder to place the desktop file in: {e.Message}"
            };
        }
        
        var pathData = new PathData
        {
            AppId = request.AppId,
            AppName = request.AppName,
            AppImagePath = appImageFilePath,
            AppImageFolder = Path.GetDirectoryName(appImageFilePath) ?? "",
            MountFolder = mountPath,
            DesktopFilePath = Path.Combine(desktopFolderPath, $"{request.AppId}.desktop")
        };

        List<string> addedFiles = [pathData.DesktopFilePath];

        try
        {
            var icons = CreateIcons(pathData);
            addedFiles.AddRange(icons);
        }
        catch (Exception e)
        {
            if (deleteMountPath)
            {
                Directory.Delete(mountPath, true);
            }
            
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the icon file(s): {e.Message}"
            };
        }

        try
        {
            if (request.AddUninstallAction)
            {
                addedFiles.Add(CreateUninstallFile(request, pathData));
            }
        }
        catch (Exception e)
        {
            if (deleteMountPath)
            {
                Directory.Delete(mountPath, true);
            }
            
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the uninstall file: {e.Message}"
            };
        }

        try
        {
            CreateDesktopFile(request, pathData);
        }
        catch (Exception e)
        {
            if (deleteMountPath)
            {
                Directory.Delete(mountPath, true);
            }
            
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the desktop file: {e.Message}"
            };
        }

        var mimeTypeSuccessful = false;
        string? mimeTypeError = null;
        
        try
        {
            if (request.CustomMimeTypeInfo != null)
            {
                mimeTypeSuccessful = CreateMimeTypeFiles(request, pathData, addedFiles, out mimeTypeError);
            }
        }
        catch (Exception e)
        {
            if (deleteMountPath)
            {
                Directory.Delete(mountPath, true);
            }
            
            return new CreateDesktopFileResponse
            {
                Success = true,
                MimeTypeSuccessful = false,
                MimeTypeError = $"Failed creating mime type file: {e.Message}",
                AddedFiles = addedFiles,
            };
        }
        
        if (deleteMountPath)
        {
            Directory.Delete(mountPath, true);
        }
        
        return new CreateDesktopFileResponse
        {
            Success = true,
            MimeTypeSuccessful = mimeTypeSuccessful,
            MimeTypeError = mimeTypeError,
            AddedFiles = addedFiles
        };
    }
    #endregion Public Methods
    
    #region Private Methods
    private static List<string> CreateIcons(PathData pathData)
    {
        var iconFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".icons");

        if (!Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }

        var hiColorFolder = Path.Combine(iconFolder, "hicolor");
        if (!Directory.Exists(hiColorFolder))
        {
            Directory.CreateDirectory(hiColorFolder);
        }

        var copyFromFolder = Path.Combine(pathData.MountFolder, "usr", "share", "icons", "hicolor");

        List<IconInfo> icons = [];
        Helper.CopyFilesRecursively(new DirectoryInfo(copyFromFolder), new DirectoryInfo(hiColorFolder), icons);
        pathData.IconPaths = icons.Select(x => x.Path).ToList();

        var primaryIcon = icons.OrderByDescending(x => x.Size).FirstOrDefault();
        if (primaryIcon != null)
        {
            var primaryIconFileName = Path.GetFileNameWithoutExtension(pathData.AppId).Replace(".", "_");
            var extension = Path.GetExtension(primaryIcon.Path);
            var targetPrimaryIcon = Path.Combine(iconFolder, $"{primaryIconFileName}{extension}");
            File.Copy(primaryIcon.Path, targetPrimaryIcon, true);
            pathData.PrimaryIcon = targetPrimaryIcon;
            pathData.IconPaths.Add(targetPrimaryIcon);
        }
        
        return pathData.IconPaths;
    }

    private static string CreateUninstallFile(CreateDesktopFileRequest request, PathData pathData)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "app-image-uninstalls");

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        pathData.UninstallFilePath = Path.Combine(folder, $"{request.AppId}.sh");
        
        var uninstallFileText = pathData.ApplyReplacements(Templates.UninstallFile);

        var paths = (request.AdditionalUninstallPaths ?? []).Concat(pathData.IconPaths ?? []);
        foreach (var path in paths)
        {
            if (Path.HasExtension(path))
            {
                uninstallFileText += Environment.NewLine + $"rm -f {path}";
            }
            else
            {
                uninstallFileText += Environment.NewLine + $"rm -rf {path}";
            }
        }

        request.CustomActions ??= [];
        request.CustomActions.Add(new CustomAction
        {
            Code = "remove",
            Name = $"Uninstall {request.AppName}",
            Command = pathData.UninstallFilePath,
            Icon = "edit-delete-symbolic"
        });

        uninstallFileText = uninstallFileText.Replace("\r\n", "\n");
        
        File.WriteAllText(pathData.UninstallFilePath, uninstallFileText + Environment.NewLine);
        File.SetUnixFileMode(pathData.UninstallFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);

        return pathData.UninstallFilePath;
    }

    private static void CreateDesktopFile(CreateDesktopFileRequest request, PathData pathData)
    {
        var desktopFile = Directory.EnumerateFiles(pathData.MountFolder, "*.desktop", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (string.IsNullOrEmpty(desktopFile))
        {
            throw new FileNotFoundException("Unable to find desktop file");
        }
        
        var desktopText = File.ReadAllLines(desktopFile);
        var execLine = desktopText.FirstOrDefault(x => x.StartsWith("Exec="));
        
        if (string.IsNullOrEmpty(execLine))
        {
            throw new InvalidOperationException("Unable to find exec line in desktop file");
        }

        execLine = execLine.Split("=", 2)[1];

        var mimeInsertIndex = 0;
        var isInDesktopEntry = true;
        var insertedMimeType = false;
        var insertedActionsType = false;
        var insertedPathFolder = false;
        
        var stringBuilder = new StringBuilder();
        foreach (var line in desktopText)
        {
            if (line.StartsWith("Actions="))
            {
                var actions = line.Split("=", 2)[1].Split(";");
                if (request.CustomActions?.Count > 0)
                {
                    actions = actions.Concat(request.CustomActions.Select(x => x.Name)).ToArray();
                }
                var actionCodeList = string.Join(";", actions);
                stringBuilder.AppendLine($"Actions={actionCodeList}");
                insertedActionsType = true;
            }
            else if (line.StartsWith("Mime=") && request.CustomMimeTypeInfo != null)
            {
                stringBuilder.AppendLine($"MimeType={request.CustomMimeTypeInfo.MimeType}");
                insertedMimeType = true;
            }
            else if (line.StartsWith("Icon=") && !string.IsNullOrEmpty(pathData.PrimaryIcon))
            {
                stringBuilder.AppendLine($"Icon={pathData.EscapedPrimaryIcon}");
            }
            else if (line.StartsWith("Path="))
            {
                stringBuilder.AppendLine($"Path={pathData.EscapedAppImageFolder}");
                insertedPathFolder = true;
            }
            else if (line.StartsWith("Name=") && isInDesktopEntry)
            {
                stringBuilder.AppendLine($"Name={request.AppName}");
            }
            else
            {
                if (line.StartsWith("[Desktop Entry]"))
                {
                    isInDesktopEntry = true;
                }
                if (line.StartsWith("[Desktop Action"))
                {
                    isInDesktopEntry = false;
                }
                if (line.StartsWith("Categories"))
                {
                    mimeInsertIndex = stringBuilder.Length;
                }
                stringBuilder.AppendLine(line.Replace(execLine, pathData.EscapedAppImagePath));
            }
        }

        if (!insertedActionsType && request.CustomActions?.Count > 0)
        {
            var actions = request.CustomActions.Select(x => x.Code);
            var actionCodeList = string.Join(";", actions);
            stringBuilder.AppendLine($"Actions={actionCodeList}");
        }
        
        if (!insertedMimeType && request.CustomMimeTypeInfo != null)
        {
            stringBuilder.Insert(mimeInsertIndex, $"MimeType={request.CustomMimeTypeInfo.MimeType}{Environment.NewLine}");
        }

        if (!insertedPathFolder)
        {
            stringBuilder.Insert(mimeInsertIndex, $"Path={pathData.EscapedAppImageFolder}{Environment.NewLine}");
        }

        foreach (var customAction in request.CustomActions ?? [])
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"[Desktop Action {customAction.Code}]");
            stringBuilder.AppendLine($"Name={customAction.Name}");
            stringBuilder.AppendLine($"Exec={pathData.ApplyReplacements(customAction.Command)}");
        }
        
        stringBuilder.AppendLine();
        File.WriteAllText(pathData.DesktopFilePath, stringBuilder.ToString());
    }

    private static bool CreateMimeTypeFiles(CreateDesktopFileRequest request, PathData pathData, List<string> addedFiles, out string? error)
    {
        var mimeType = request.CustomMimeTypeInfo?.MimeType;
        var description = request.CustomMimeTypeInfo?.Description;
        var globPattern = request.CustomMimeTypeInfo?.GlobPattern;

        if (string.IsNullOrEmpty(mimeType) || !mimeType.Contains('/') || mimeType.Split("/").Length != 2)
        {
            throw new InvalidOperationException("Invalid mime type");
        }

        if (string.IsNullOrEmpty(globPattern) || !globPattern.StartsWith("*."))
        {
            throw new InvalidOperationException("Invalid glob pattern");
        }
        
        var mimeFolder = GetMimePackagesFolder();
        if (!Directory.Exists(mimeFolder))
        {
            Directory.CreateDirectory(mimeFolder);
        }

        var mimePath = GetMimeFilePath(mimeFolder, mimeType);
        if (File.Exists(mimePath))
        {
            File.Delete(mimePath);
        }

        var mimeDetails = Templates.MimeTypeFile;
        mimeDetails = mimeDetails.Replace("%MimeType%", mimeType);
        mimeDetails = mimeDetails.Replace("%Description%", description);
        mimeDetails = mimeDetails.Replace("%GlobPattern%", globPattern);
        mimeDetails = mimeDetails.Replace("\r\n", "\n");
        File.WriteAllText(mimePath, mimeDetails);
        addedFiles.Add(mimePath);

        var mimeListPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mimeapps.list");

        var parser = new FileIniDataParser();
        var data = new IniData();
        if (File.Exists(mimeListPath))
        {
            data = parser.ReadFile(mimeListPath);
        }

        if (!data.Sections.ContainsSection("Default Applications"))
        {
            data.Sections.AddSection("Default Applications");
        }
        data["Default Applications"][mimeType] = Path.GetFileName(pathData.DesktopFilePath);

        if (request.CustomMimeTypeInfo?.AutoAssociate == true)
        {
            if (!data.Sections.ContainsSection("Added Associations"))
            {
                data.Sections.AddSection("Added Associations");
            }
            data["Added Associations"][mimeType] = Path.GetFileName(pathData.DesktopFilePath);
        }

        var formatter = new DefaultIniDataFormatter
        {
            Configuration =
            {
                AssigmentSpacer = ""
            }
        };
        var iniString = data.ToString(formatter) ?? "";
        File.WriteAllText(mimeListPath, iniString);

        if (!UpdateMimeDatabase())
        {
            error = "Error updating mime database";
        }
        else if (!UpdateDesktopDatabase())
        {
            error = "Error updating desktop database";
        }
        else
        {
            error = "";
        }

        return string.IsNullOrEmpty(error);
    }

    private static bool UpdateMimeDatabase()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "update-mime-database",
            Arguments = GetMimeFolder(),
            RedirectStandardOutput = false,
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        try
        {
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"Console App exited with code: {process.ExitCode}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting or running console app: {ex.Message}");
            return false;
        }
    }
    
    private static bool UpdateDesktopDatabase()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "update-desktop-database",
            RedirectStandardOutput = false,
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        try
        {
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"Console App exited with code: {process.ExitCode}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting or running console app: {ex.Message}");
            return false;
        }
    }

    private static string GetDesktopFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
    }

    private static string GetDesktopFileName(string desktopFolder, string appId)
    {
        return Path.Combine(desktopFolder, $"{appId}.desktop");
    }

    private static string GetMimeFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mime");
    }
    
    private static string GetMimePackagesFolder()
    {
        return Path.Combine(GetMimeFolder(), "packages");
    }

    private static string GetMimeFilePath(string mimeFolder, string mimeType)
    {
        return Path.Combine(mimeFolder, mimeType.Split("/")[1] + ".xml");
    }

    private static string ExtractAppImage(string appImagePath)
    {
        var originalCurrentDirectory = Environment.CurrentDirectory;
        
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, true);
        }
        Directory.CreateDirectory(tempPath);
        Environment.CurrentDirectory = tempPath;
            
        var tempAppImagePath = Path.Combine(tempPath, "test.AppImage");
        File.Copy(appImagePath, tempAppImagePath);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tempAppImagePath,
                Arguments = "--appimage-extract",
                UseShellExecute = true,
                CreateNoWindow = true,
                WorkingDirectory = tempPath,
            }
        };

        process.Start();
        process.WaitForExit();

        Environment.CurrentDirectory = originalCurrentDirectory;

        return Path.Combine(tempPath, "squashfs-root");
    }
    #endregion Private Methods
}