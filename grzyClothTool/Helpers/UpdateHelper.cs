using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Sentry;

namespace grzyClothTool.Helpers;

public static class UpdateHelper
{
    private static readonly HttpClient _httpClient;
    private static readonly string _exeLocation;
    private static readonly string _updateFolder;
    private static Mutex _appMutex;
    private const int MAX_RETRIES = 5;
    private const int INITIAL_RETRY_DELAY_MS = 100;

    static UpdateHelper()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _exeLocation = GetExeLocation();
        _updateFolder = Path.Combine(Path.GetTempPath(), "grzyclothtool_update");
        
        _appMutex = new Mutex(true, "grzyClothTool_SingleInstance", out bool createdNew);
        
        if (createdNew)
        {
            SafeCleanupUpdateFolder();
        }
    }

    private static string GetExeLocation()
    {
        string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
        var assemblyLocation = Path.Join(AppContext.BaseDirectory, $"{assemblyName}.exe");

        return assemblyLocation;
    }

    private static void SafeCleanupUpdateFolder()
    {
        try
        {
            if (Directory.Exists(_updateFolder))
            {
                try
                {
                    var extractFolders = Directory.GetDirectories(_updateFolder, "extract_*");
                    foreach (var folder in extractFolders)
                    {
                        try
                        {
                            SafeDeleteDirectory(folder, maxAttempts: 2);
                        }
                        catch (Exception ex)
                        {
                            LogCleanupException("SafeCleanupUpdateFolder:extract", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogCleanupException("SafeCleanupUpdateFolder:enumerate", ex);
                }

                try
                {
                    if (Directory.GetFileSystemEntries(_updateFolder).Length == 0)
                    {
                        Directory.Delete(_updateFolder);
                    }
                }
                catch (Exception ex)
                {
                    LogCleanupException("SafeCleanupUpdateFolder:delete", ex);
                }
            }
        }
        catch (Exception ex)
        {
            LogCleanupException("SafeCleanupUpdateFolder:outer", ex);
        }
    }

    private static void LogCleanupException(string context, Exception ex)
    {
        Debug.WriteLine($"UpdateHelper[{context}]: {ex.Message}");
        try { SentrySdk.CaptureException(ex); } catch { }
    }

    private static void SafeDeleteDirectory(string path, int maxAttempts = MAX_RETRIES)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    SetAttributesNormal(dirInfo);
                    
                    Directory.Delete(path, true);
                    return;
                }
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt < maxAttempts - 1)
                {
                    Thread.Sleep(INITIAL_RETRY_DELAY_MS * (int)Math.Pow(2, attempt));
                }
                else
                {
                    throw;
                }
            }
            catch (IOException)
            {
                if (attempt < maxAttempts - 1)
                {
                    Thread.Sleep(INITIAL_RETRY_DELAY_MS * (int)Math.Pow(2, attempt));
                }
                else
                {
                    throw;
                }
            }
        }
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        try
        {
            foreach (var subDir in dir.GetDirectories())
            {
                SetAttributesNormal(subDir);
            }
            foreach (var file in dir.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
            }
        }
        catch (Exception ex)
        {
            LogCleanupException("SetAttributesNormal", ex);
        }
    }

    private static async Task<T> RetryOperation<T>(Func<Task<T>> operation, int maxAttempts = MAX_RETRIES, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts - 1 && !cancellationToken.IsCancellationRequested)
            {
                int delay = INITIAL_RETRY_DELAY_MS * (int)Math.Pow(2, attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
        return await operation();
    }

    private static void SafeDeleteFile(string filePath, int maxAttempts = MAX_RETRIES)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                if (attempt < maxAttempts - 1)
                {
                    Thread.Sleep(INITIAL_RETRY_DELAY_MS * (int)Math.Pow(2, attempt));
                }
            }
        }
    }

    public static string GetCurrentVersion()
    {
        return FileVersionInfo.GetVersionInfo(_exeLocation).FileVersion;
    }

    public async static Task CheckForUpdates()
    {
        string[] args = Environment.GetCommandLineArgs();
        
        if (args.Contains("--skipUpdate"))
        {
            var removeTempFilesArg = args.FirstOrDefault(arg => arg.StartsWith("--removeTempFiles"));
            if (removeTempFilesArg != null)
            {
                App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.Completing"));
                
                var oldExePath = removeTempFilesArg.Split('=')[1].Trim('"');
                
                RemoveTempFilesAndRestart(oldExePath);
                
                await Task.Delay(Timeout.Infinite);
            }

            return;
        }

        CancellationTokenSource cts = null;
        try
        {
            cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            
            string currentVersion = GetCurrentVersion();
                
            App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.Checking"));
            var latestVersion = await RetryOperation(async () => await GetLatestVersion(), maxAttempts: 2, cts.Token);

            if (latestVersion is null)
            {
                App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.CouldNotCheck"));
                await Task.Delay(500, cts.Token);
                return;
            }

            if(latestVersion == currentVersion)
            {
                App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.UpToDate"));
                await Task.Delay(500, cts.Token);
                return;
            }

            App.splashScreen.AddMessage(LocalizationHelper.GetFormat("Str.UpdateHelper.DownloadingFormat", latestVersion));
            
            await DownloadUpdate(latestVersion, cts.Token);
        }
        catch (OperationCanceledException)
        {
            App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.TimedOut"));
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.CheckFailed"));
            try
            {
                await File.WriteAllTextAsync("update_check_failed.log", $"[{DateTime.Now}]\n{ex}");
            }
            catch (Exception logEx)
            {
                LogCleanupException("CheckForUpdates:writeLog", logEx);
            }
            try { SentrySdk.CaptureException(ex); } catch { }
            await Task.Delay(1000);
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private async static Task<string> GetLatestVersion()
    {
        try
        {
            string url = "https://raw.githubusercontent.com/grzybeek/grzyClothTool/master/grzyClothTool/grzyClothTool.csproj";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            // Parse the content as XML
            XDocument doc = XDocument.Parse(content);
            XElement variableElement = doc.Root?.Element("PropertyGroup")?.Element("FileVersion");

            return variableElement?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static async Task DownloadUpdate(string version, CancellationToken cancellationToken)
    {
        string url = $"https://github.com/grzybeek/grzyClothTool/releases/download/v{version}/grzyClothTool.zip";
        string downloadZip = Path.Combine(_updateFolder, "grzyClothTool.zip");

        try
        {
            Directory.CreateDirectory(_updateFolder);
            
            SafeDeleteFile(downloadZip, maxAttempts: 3);

            await RetryOperation(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var fileStream = new FileStream(downloadZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await response.Content.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
                
                return true;
            }, maxAttempts: 3, cancellationToken);
            
            App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.DownloadComplete"));
            await Task.Delay(500, cancellationToken);
            
            ExtractAndRunUpdatedApp();
        }
        catch (OperationCanceledException)
        {
            App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.DownloadCancelled"));
            await Task.Delay(1500);
        }
        catch(Exception ex)
        {
            try
            {
                await File.WriteAllTextAsync("download_failed.log", $"[{DateTime.Now}]\n{ex}");
            }
            catch (Exception logEx)
            {
                LogCleanupException("DownloadUpdate:writeLog", logEx);
            }
            try { SentrySdk.CaptureException(ex); } catch { }

            App.splashScreen.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.DownloadFailed"));
            await Task.Delay(2000);
        }
    }

    private static void ExtractAndRunUpdatedApp()
    {
        try
        {
            string downloadZip = Path.Combine(_updateFolder, "grzyClothTool.zip");

            if (!File.Exists(downloadZip))
            {
                throw new FileNotFoundException("Update package not found.");
            }

            string extractFolder = Path.Combine(_updateFolder, $"extract_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(extractFolder);

            bool extracted = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(downloadZip, extractFolder, overwriteFiles: false);
                    extracted = true;
                    break;
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(500);
                }
            }

            if (!extracted)
            {
                throw new IOException("Failed to extract update package after multiple attempts.");
            }
            
            SafeDeleteFile(downloadZip);

            var newExeLocation = Path.Combine(extractFolder, "grzyClothTool.exe");
            
            if (!File.Exists(newExeLocation))
            {
                throw new FileNotFoundException("Updated executable not found in package.");
            }

            _appMutex?.ReleaseMutex();
            _appMutex?.Dispose();

            // Run exe with args
            ProcessStartInfo startInfo = new()
            {
                FileName = newExeLocation,
                ArgumentList = { "--skipUpdate", $"--removeTempFiles=\"{_exeLocation}\"" },
                UseShellExecute = true,
                WorkingDirectory = extractFolder
            };
            
            Process.Start(startInfo);

            Thread.Sleep(500);
            
            App.splashScreen?.Shutdown();
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText("extract_failed.log", $"[{DateTime.Now}]\n{ex}");
            }
            catch (Exception logEx)
            {
                LogCleanupException("ExtractAndRunUpdatedApp:writeLog", logEx);
            }
            try { SentrySdk.CaptureException(ex); } catch { }

            App.splashScreen?.AddMessage(LocalizationHelper.Get("Str.UpdateHelper.InstallationFailed"));
            Task.Delay(2500).Wait();
        }
    }

    private static void RemoveTempFilesAndRestart(string oldExeLocation)
    {
        try
        {
            // Kill all other grzyClothTool processes to ensure no file locks
            KillAllOtherInstances();
            
            Thread.Sleep(2000);
            
            var oldDir = Path.GetDirectoryName(oldExeLocation);
            if (string.IsNullOrEmpty(oldDir) || !Directory.Exists(oldDir))
            {
                ForceShutdown();
                return;
            }

            // Remove old .exe and .dll.config from oldExeLocation
            string[] fileExtensions = [".exe", ".dll.config"];
            foreach (var extension in fileExtensions)
            {
                var pattern = $"grzyClothTool{extension}";
                var filesToDelete = Directory.GetFiles(oldDir, pattern);
                foreach (var file in filesToDelete)
                {
                    SafeDeleteFile(file);
                }
            }

            string[] files = Directory.GetFiles(AppContext.BaseDirectory);
            int filesMoved = 0;
            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(oldDir, fileName);
                    
                    if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    SafeDeleteFile(destPath);
                    
                    for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
                    {
                        try
                        {
                            File.Move(file, destPath);
                            filesMoved++;
                            break;
                        }
                        catch (IOException) when (attempt < MAX_RETRIES - 1)
                        {
                            Thread.Sleep(INITIAL_RETRY_DELAY_MS * (int)Math.Pow(2, attempt));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Continue with next file even if one fails
                    LogCleanupException("RemoveTempFiles:moveFile", ex);
                }
            }

            if (filesMoved > 0)
            {
                var finalExePath = Path.Combine(oldDir, "grzyClothTool.exe");
                if (File.Exists(finalExePath))
                {
                    ProcessStartInfo startInfo = new()
                    {
                        FileName = finalExePath,
                        UseShellExecute = true,
                        WorkingDirectory = oldDir
                    };
                    
                    Process.Start(startInfo);
                    
                    Thread.Sleep(500);
                }
            }

            try
            {
                if (Directory.Exists(_updateFolder))
                {
                    var extractFolders = Directory.GetDirectories(_updateFolder, "extract_*");
                    foreach (var folder in extractFolders)
                    {
                        try
                        {
                            SafeDeleteDirectory(folder, maxAttempts: 2);
                        }
                        catch (Exception ex)
                        {
                            LogCleanupException("RemoveTempFiles:delete-extract", ex);
                        }
                    }

                    if (Directory.GetFileSystemEntries(_updateFolder).Length == 0)
                    {
                        Directory.Delete(_updateFolder);
                    }
                }
            }
            catch (Exception ex)
            {
                LogCleanupException("RemoveTempFiles:outer", ex);
            }

            ForceShutdown();
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "grzyclothtool_cleanup_error.log"),
                    $"[{DateTime.Now}]\n{ex}");
            }
            catch (Exception logEx)
            {
                LogCleanupException("RemoveTempFiles:writeLog", logEx);
            }
            try { SentrySdk.CaptureException(ex); } catch { }

            ForceShutdown();
        }
    }

    private static void KillAllOtherInstances()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var currentProcessId = currentProcess.Id;
            
            var processes = Process.GetProcessesByName("grzyClothTool");
            
            foreach (var process in processes)
            {
                try
                {
                    // Skip the current process (the temp updater instance)
                    if (process.Id == currentProcessId)
                    {
                        continue;
                    }
                    
                    if (!process.HasExited)
                    {
                        process.Kill();
                        
                        process.WaitForExit(2000);
                    }
                    
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    // Continue with other processes even if one fails
                    LogCleanupException("KillAllOtherInstances:process", ex);
                }
            }
        }
        catch (Exception ex)
        {
            // Continue update even if we can't kill processes
            LogCleanupException("KillAllOtherInstances:outer", ex);
        }
    }

    private static void ForceShutdown()
    {
        try
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    App.splashScreen?.Shutdown();
                }
                catch { }
                
                try
                {
                    Application.Current?.Shutdown();
                }
                catch { }
            });
            
            Thread.Sleep(200);
        }
        catch { }
        
        try
        {
            Environment.Exit(0);
        }
        catch { }
    }
}
