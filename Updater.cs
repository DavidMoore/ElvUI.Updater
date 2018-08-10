﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using Squirrel;

namespace SadRobot.ElvUI
{
    class Updater
    {
        static readonly Regex regex = new Regex(@"/downloads/elvui-([0-9]+\.[0-9]+).zip");

        internal static async Task MainAsync(IProgress<UpdateProgress> progress)
        {
            try
            {
                progress.Report("Checking for program updates...");
                using (var manager = await UpdateManager.GitHubUpdateManager("https://github.com/DavidMoore/ElvUI.Updater", "ElvUI Updater"))
                {
                    if (manager.IsInstalledApp)
                    {
                        await manager.UpdateApp(x => progress.Report("Updating...", x));
                        progress.Report("Updated.", 100);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("\rError updating: " + e);
            }

            try
            {
                using (var client = new HttpClient())
                {
                    progress.Report("Checking for ElVUI version...");

                    var html = await client.GetStringAsync("https://www.tukui.org/download.php?ui=elvui");

                    var match = regex.Match(html);

                    var version = match.Groups[1].Value;

                    progress.Report("Latest ElVUI is " + version);

                    var link = "https://www.tukui.org" + match.Value;

                    progress.Report("Locating World of Warcraft...");
                    string installPath;
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (var key = hklm.OpenSubKey(@"SOFTWARE\Blizzard Entertainment\World of Warcraft"))
                    {
                        installPath = (string)key?.GetValue("InstallPath") ?? throw new UpdateException("Couldn't find the World of Warcraft install information");
                    }
                    if (string.IsNullOrWhiteSpace(installPath)) throw new UpdateException("Couldn't locate the install path");

                    var installDirectory = new DirectoryInfo(installPath);
                    if (!installDirectory.Exists) throw new UpdateException($"WoW doesn't seem to be installed at {installPath}");

                    var addOnsPath = Path.Combine(installDirectory.FullName, @"Interface\AddOns\");
                    var elvuiFolder = Path.Combine(addOnsPath, "ElvUI");
                    var elvuiConfigFolder = elvuiFolder + "_Config";

                    var temp = Path.GetTempFileName();

                    try
                    {
                        progress.Report("Downloading...");

                        var buffer = new byte[1024 * 64];

                        using (var destination = File.Open(temp, FileMode.Truncate, FileAccess.Write, FileShare.Read))
                        using (var response = await client.GetAsync(link, HttpCompletionOption.ResponseHeadersRead))
                        {
                            var timestamp = response.Content.Headers.LastModified ?? response.Headers.Date;
                            var etag = response.Headers.ETag;

                            var length = response.Content.Headers.ContentLength ?? -1;
                            var lengthInMb = length == -1 ? -1 : (double)length / 1024 / 1024;

                            double bytesDownloaded = 0;

                            using (var source = await response.Content.ReadAsStreamAsync())
                            {
                                int bytesRead;
                                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await destination.WriteAsync(buffer, 0, bytesRead);
                                    bytesDownloaded += bytesRead;
                                    if (length <= 0) continue;

                                    var downloadedMegs = bytesDownloaded / 1024 / 1024;
                                    var percent = (int)Math.Floor(bytesDownloaded / length * 100);
                                    progress.Report("Downloaded {0:F2} MB of {1:F2} MB", percent, downloadedMegs, lengthInMb);
                                }
                            }

                            progress.Report("Finished downloading", 100);
                        }

                        progress.Report("Cleaning out previous ElVUI");
                        if (Directory.Exists(elvuiFolder)) Directory.Delete(elvuiFolder, true);
                        if (Directory.Exists(elvuiConfigFolder)) Directory.Delete(elvuiConfigFolder, true);

                        progress.Report("Extracting...");
                        using (var zip = File.Open(temp, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var archive = new ZipArchive(zip, ZipArchiveMode.Read, true))
                        {
                            var directory = new DirectoryInfo(addOnsPath);
                            if (!directory.Exists) directory.Create();

                            foreach (var entry in archive.Entries)
                            {
                                var fileDestination = Path.GetFullPath(Path.Combine(directory.FullName, entry.FullName));

                                if (!fileDestination.StartsWith(directory.FullName, StringComparison.OrdinalIgnoreCase)) throw new UpdateException("Can't extract zip contents outside of destination folder");

                                if (Path.GetFileName(fileDestination).Length == 0)
                                {
                                    // This is a directory that must be created
                                    if (entry.Length != 0) throw new UpdateException("Directory in zip file has unexpected data");
                                    Directory.CreateDirectory(fileDestination);
                                }
                                else
                                {
                                    // This is a file that needs to be extracted

                                    // Make sure the destination directory exists first
                                    var destinationDirectory = Path.GetDirectoryName(fileDestination);
                                    if (destinationDirectory == null) throw new UpdateException($"Couldn't get the directory for entry {entry.FullName}: {fileDestination}");
                                    Directory.CreateDirectory(destinationDirectory);

                                    // Now we can extract it out
                                    using (var file = File.Open(fileDestination, FileMode.Create, FileAccess.Write, FileShare.Read))
                                    using (var entryStream = entry.Open())
                                    {
                                        await entryStream.CopyToAsync(file);
                                    }
                                }
                            }
                        }

                        progress.Report("Finished updating to " + version);
                    }
                    finally
                    {
                        if (File.Exists(temp))
                        {
                            try
                            {
                                File.Delete(temp);
                            }
                            catch (Exception e)
                            {
                                throw new UpdateException($"Warning: Couldn't clean up the temporary download @ {temp}", e);
                            }
                        }
                    }
                }
            }
            catch (AggregateException ae)
            {
                var sb = new StringBuilder().Append(ae.Message);

                foreach (var exception in ae.InnerExceptions)
                {
                    Trace.TraceWarning(exception.ToString());
                }

                progress.Report(sb.ToString(), 100);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("There was a problem when updating: " + ex);
                progress.Report("Error: " + ex.Message, 100);
            }
        }
    }
}