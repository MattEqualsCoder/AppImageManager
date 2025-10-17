namespace AppImageManager;

internal static class Helper
{
    internal static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target, List<IconInfo> icons) 
    {
        foreach (var dir in source.GetDirectories())
        {
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name), icons);
        }
            
        foreach (var file in source.GetFiles())
        {
            var destination = Path.Combine(target.FullName, file.Name);
            file.CopyTo(destination, overwrite: true);

            var fileExtension = Path.GetExtension(file.Name).ToLower();

            if (fileExtension == ".svg")
            {
                icons.Add(new IconInfo()
                {
                    Size = Int32.MaxValue,
                    Path = destination
                });
            }
            else if (fileExtension == ".png")
            {
                var sizeFolder = file.Directory?.Parent?.Name;
                if (sizeFolder?.Contains("x") == true && int.TryParse(sizeFolder.Split('x')[1], out var size))
                {
                    icons.Add(new IconInfo()
                    {
                        Size = size,
                        Path = destination
                    });
                }
            }
        }
    }
    
    internal static async Task<(bool, string?)> DownloadFileAsyncAttempt(string url, string target, int attemptNumber = 0, int totalAttempts = 3)
    {
        
        using var httpClient = new HttpClient();

        try
        {
            await using var downloadStream = await httpClient.GetStreamAsync(url);
            await using var fileStream = new FileStream(target, FileMode.Create);
            await downloadStream.CopyToAsync(fileStream);
            return (true, null);
        }
        catch (Exception ex)
        {
            if (attemptNumber < totalAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attemptNumber));
                return await DownloadFileAsyncAttempt(url, target, attemptNumber + 1, totalAttempts);
            }
            else
            {
                return (false, $"Download failed: {ex.Message}");
            }
        }
    }
}