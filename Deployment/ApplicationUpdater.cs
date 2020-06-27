using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using Microsoft.Win32;
using SadRobot.ElvUI.Deployment.Native;

namespace SadRobot.ElvUI.Deployment
{
    class ApplicationUpdater
    {
        const string updateSuffix = ".update.exe";

        readonly string defaultUri;
        static readonly Assembly assembly;
        static readonly FileVersionInfo fileVersionInfo = Process.GetCurrentProcess().MainModule.FileVersionInfo;
        static readonly HttpClientHandler handler;
        static readonly HttpClient client;
        static readonly Guid guid;
        readonly Progress<ProgressModel> progress;
        readonly CancellationToken cancellationToken;

        static ApplicationUpdater()
        {
            handler = new HttpClientHandler
            {
                AutomaticDecompression =  DecompressionMethods.Deflate | DecompressionMethods.GZip
            };
            
            client = new HttpClient(handler);
            assembly = typeof(ApplicationUpdater).Assembly;

            var guidAttribute = assembly.GetCustomAttribute<GuidAttribute>();

            guid = new Guid(guidAttribute.Value);
        }

        public ApplicationUpdater(string defaultUri = null, CancellationToken cancellationToken = default)
        {
            this.defaultUri = defaultUri;
            progress = new Progress<ProgressModel>();
            this.cancellationToken = cancellationToken;
        }

        public async Task UninstallAsync(UninstallArgs args = null)
        {
            var productName = args?.ProductName ?? fileVersionInfo.ProductName;

            // Remove the Add / Remove Programs information
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + guid, false);

            // Start Menu shortcut
            var programsFolder = new DirectoryInfo( Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.Programs)));
            var shortcutPath = new FileInfo(Path.Combine(programsFolder.FullName, productName + ".lnk"));
            if(shortcutPath.Exists) shortcutPath.Delete();

            // Desktop shortcut
            var desktopFolder = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
            var desktopShortcut = new FileInfo(Path.Combine(desktopFolder.FullName, productName + ".lnk"));
            if( desktopShortcut.Exists ) desktopShortcut.Delete();

            if (args == null || !args.Silent)
            {
                MessageBox.Show("Uninstalled successfully", fileVersionInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public async Task InstallAsync(InstallArgs args = null)
        {
            var name = args?.Name ?? Path.GetFileNameWithoutExtension(fileVersionInfo.FileName);
            var company = args?.Company ?? fileVersionInfo.CompanyName;
            var path = Environment.ExpandEnvironmentVariables( args?.Path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", name) );
            var filename = Path.Combine(path, Path.GetFileName(fileVersionInfo.FileName));
            var productName = args?.ProductName ?? fileVersionInfo.ProductName;
            
            // Copy the file to the destination if it doesn't already exist
            var fileInfo = new FileInfo(filename);
            if (!fileInfo.Directory.Exists) fileInfo.Directory.Create();
            File.Copy(fileVersionInfo.FileName, fileInfo.FullName, true);

            // https://docs.microsoft.com/en-nz/windows/win32/msi/uninstall-registry-key
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + guid))
            {
                var stringsToWrite = new[] {
                    new { Key = "DisplayIcon", Value = $"{filename},0" },
                    new { Key = "DisplayName", Value = args?.ProductName ?? fileVersionInfo.ProductName },
                    new { Key = "DisplayVersion", Value = fileVersionInfo.FileVersion },
                    new { Key = "InstallDate", Value = DateTime.Now.ToString("yyyyMMdd") },
                    new { Key = "InstallLocation", Value = path },
                    new { Key = "Publisher", Value = company },
                    new { Key = "QuietUninstallString", Value = $"\"{filename}\" uninstall silent"},
                    new { Key = "UninstallString", Value = $"\"{filename}\" uninstall"},
                    new { Key = "HelpLink", Value = args?.Url ?? "" },
                    new { Key = "Comments", Value = "Comments" }
                };

                var dwordsToWrite = new[] {
                    new { Key = "EstimatedSize", Value = (int)(new FileInfo(filename).Length / 1024) },
                    new { Key = "NoModify", Value = 1 },
                    new { Key = "NoRepair", Value = 1 },
                    new { Key = "Language", Value = 0x0409 },
                };

                foreach (var kvp in stringsToWrite)
                {
                    key.SetValue(kvp.Key, kvp.Value, RegistryValueKind.String);
                }
                foreach (var kvp in dwordsToWrite)
                {
                    key.SetValue(kvp.Key, kvp.Value, RegistryValueKind.DWord);
                }
            }

            // Start Menu shortcut
            var programsFolder = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs)));
            var shortcutPath = new FileInfo(Path.Combine(programsFolder.FullName, productName + ".lnk"));
            CreateShortcut(shortcutPath.FullName, fileVersionInfo.FileName);
            
            // Desktop shortcut
            var desktopFolder = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
            var desktopShortcut = new FileInfo(Path.Combine(desktopFolder.FullName, productName + ".lnk"));
            CreateShortcut(desktopShortcut.FullName, fileVersionInfo.FileName);
        }

        void CreateShortcut(string path, string target)
        {
            // ReSharper disable SuspiciousTypeConversion.Global

            var shortcut = (IShellLinkW)new CShellLink();
            
            try
            {
                shortcut.SetWorkingDirectory(Path.GetDirectoryName(target));
                shortcut.SetPath(target);
                ((IPersistFile)shortcut).Save(path, true);
            }
            finally
            {
                Marshal.ReleaseComObject(shortcut);
            }

            // ReSharper restore SuspiciousTypeConversion.Global
        }

        public async Task LaunchAsync(LaunchArgs args = null)
        {
            var commandLine = new StringBuilder();

            if (args?.Target != null)
            {
                commandLine.Append(args.Target);
            }
            else
            {
                commandLine.Append(fileVersionInfo.FileName);
            }

            if (args?.Args != null)
            {
                commandLine.Append(" ").Append(args.Args);
            }

            var normalPriorityClass = 0x0020;
            var processInformation = new ProcessInformation();
            var startupInfo = new StartupInfo();
            var processSecurity = new SecurityAttributes();
            var threadSecurity = new SecurityAttributes();

            processSecurity.nLength = Marshal.SizeOf(processSecurity);
            threadSecurity.nLength = Marshal.SizeOf(threadSecurity);

            if (ProcessManager.CreateProcess(null, commandLine, processSecurity, threadSecurity, false, normalPriorityClass, IntPtr.Zero, null, startupInfo, processInformation))
            {
                // Process was created successfully
                return;
            }

            // We couldn't create the process, so raise an exception with the details.
            throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        public async Task UpdateAsync(UpdateCheckArgs args)
        {
            // Check for available update
            var update = await CheckForUpdateAsync(args);
            if (update != null)
            {
                // Download the update side by side
                var file = await DownloadAsync(update, progress, cancellationToken);

                // Launch the downloaded update
                await LaunchAsync(new LaunchArgs(file.Filename));
            }
        }

        public async Task<TempFile> DownloadAsync(UpdateInfo info, IProgress<ProgressModel> progress, CancellationToken cancellationToken = default)
        {
            using (var response = await client.GetAsync(info.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    progress.Report(new ProgressModel(ProgressState.Ready, "Update cancelled. Ready.", 100));
                    return null;
                }

                var length = response.Content.Headers.ContentLength;
                double lengthInMb = !length.HasValue ? -1 : (double)length.Value / 1024 / 1024;
                double bytesDownloaded = 0;
                
                // Download next to the executing .exe but with the extension .update.exe
                var fileInfo = new FileInfo( Path.ChangeExtension(fileVersionInfo.FileName, updateSuffix));
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var file = File.Open(fileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    var buffer = new byte[65535 * 4];

                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    while (bytesRead != 0)
                    {
                        await file.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        bytesDownloaded += bytesRead;

                        if (length.HasValue)
                        {
                            double downloadedMegs = bytesDownloaded / 1024 / 1024;
                            var percent = (int)Math.Floor((bytesDownloaded / length.Value) * 100);
                            var status = string.Format(CultureInfo.CurrentUICulture, "Downloaded {0:F2} MB of {1:F2} MB", downloadedMegs, lengthInMb);
                            progress.Report(new ProgressModel(ProgressState.Updating, status, percent));
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            progress.Report(new ProgressModel(ProgressState.Ready, "Update cancelled. Ready.", 100));
                            return null;
                        }

                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    }
                }

                var model = new TempFile
                {
                    Asset = info.Name,
                    Filename = fileInfo.FullName
                };

                return model;
            }

        }

        public async Task<UpdateInfo> CheckForUpdateAsync(UpdateCheckArgs args = null)
        {
            var assetName = args?.AssetName ?? Path.GetFileName(fileVersionInfo.FileName);
            var allowPreRelease = args != null && args.AllowPreRelease;
            var ignoreTags = args?.IgnoreTags ?? new string[] { };
            var uri = args?.Uri ?? defaultUri;
            
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(assetName, fileVersionInfo.FileVersion));

            // Get the latest releases from GitHub
            using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
            {
                var content = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                var serializer = new JavaScriptSerializer();
                var results = serializer.Deserialize<List<GitHubRelease>>(content);

                var latest = results.FirstOrDefault(x => (allowPreRelease || !x.prerelease) &&
                                                         !ignoreTags.Any(tag => tag.Equals(x.tag_name, StringComparison.OrdinalIgnoreCase) ));
                if (latest == null)
                {
                    Trace.TraceWarning("Couldn't find a release from the list returned by GitHub");
                    return null;
                }

                var asset = latest.assets.FirstOrDefault(x => x.name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
                if (asset == null)
                {
                    Trace.TraceWarning($"Couldn't find '${assetName}' in the release assets for " + latest.name);
                    return null;
                }

                var info = new UpdateInfo
                {
                    Version = new SemanticVersion(latest.tag_name),
                    Uri = asset.browser_download_url.ToString(),
                    IsPreRelease = latest.prerelease,
                    Name = asset.name,
                    CurrentVersion = new SemanticVersion(fileVersionInfo.ProductVersion)
                };

                return info;
            }
        }

        void WaitForOtherProcesses(string processName = null)
        {
            var currentProcess = Process.GetCurrentProcess();
            processName = processName ?? currentProcess.ProcessName;
            var processes = Process.GetProcessesByName(processName).Where(x => x.Id != currentProcess.Id).ToList();
            foreach (var process in processes)
            {
                if (process.HasExited) continue;
                process.WaitForExit(2000);
            }
        }

        public async Task ApplyUpdateAsync(ApplyUpdateArgs args)
        {
            var source = fileVersionInfo.FileName;
            var destination = args.Target;

            // If no destination was specified, default to overwrite current path, trimmming ".update.exe" to ".exe"
            if (string.IsNullOrWhiteSpace(destination))
            {
                destination = fileVersionInfo.FileName;
                if (destination.EndsWith(updateSuffix))
                {
                    destination = destination.Substring(0, destination.Length - updateSuffix.Length) + ".exe";
                }
            }

            // Make sure the destination exe doesn't have any running processes that would prevent an overwrite
            WaitForOtherProcesses( Path.GetFileNameWithoutExtension(destination) );
            
            File.Copy(source, destination, true);
            
            // Launch the updated app
            if (args.Launch)
            {
                // Add arguments to delete the temporary exe now that the update is applied
                await LaunchAsync(new LaunchArgs { Target = destination });
            }
        }

        class GitHubRelease
        {
            public string name { get; set; }

            public string tag_name { get; set; }

            public ICollection<GitHubAsset> assets { get; set; }

            public bool prerelease { get; set; }
        }

        class GitHubAsset
        {
            public long id { get; set; }

            public string name { get; set; }

            public Uri browser_download_url { get; set; }

            public string content_type { get; set; }

            public long size { get; set; }

            public DateTimeOffset created_at { get; set; }

            public DateTimeOffset updated_at { get; set; }
        }

        public async Task CleanupAsync(CleanupArgs args)
        {
            if (args == null || args.Target == null) return;
            WaitForOtherProcesses();
            File.Delete(args.Target);
        }
    }
}