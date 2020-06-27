using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SadRobot.ElvUI.Deployment;

namespace SadRobot.ElvUI
{
    static class EntryPoint
    {
        internal static async Task<int> MainAsync(CommandLineArguments cmd)
        {
            var updater = new ApplicationUpdater("https://api.github.com/repos/DavidMoore/ElvUI.Updater/releases");
            
            switch (cmd.Command)
            {
                case Command.None:
                    break;

                case Command.Cleanup:
                    var cleanupArgs = new CleanupArgs();
                    cleanupArgs.Target = cmd.Target;
                    await updater.CleanupAsync(cleanupArgs);
                    break;

                case Command.Install:
                    await updater.InstallAsync();
                    break;
                    
                case Command.Update:
                    // Check, download and launch an update, then return immediately so the update can run.
                    var updateArgs = new UpdateCheckArgs();
                    updateArgs.AllowPreRelease = true;
                    await updater.UpdateAsync(updateArgs);
                    return 0;

                case Command.ApplyUpdate:
                    // We enter here when a downloaded update is running so it can overwrite the old exe
                    var applyUpdateArgs = new ApplyUpdateArgs();
                    applyUpdateArgs.Launch = cmd.Launch;
                    await updater.ApplyUpdateAsync(applyUpdateArgs);
                    break;

                case Command.Uninstall:
                    var uninstallArgs = new UninstallArgs();
                    uninstallArgs.Silent = cmd.Silent;
                    await updater.UninstallAsync(uninstallArgs);
                    return 0;

                case Command.Service:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return 1;
        }

        [STAThread]
        internal static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            
            var cmd = new CommandLineArguments(args);
            
            if (cmd.Command != Command.None)
            {
                return MainAsync(cmd).GetAwaiter().GetResult();
            }
            
            if (args.Length > 0)
            {
                if (cmd.Silent)
                {
                    try
                    {
                        Trace.TraceInformation("Running in silent mode");
                        var progress = new Progress<UpdateProgress>();
                        progress.ProgressChanged += OnProgressChanged;
                        Updater.MainAsync(progress).GetAwaiter().GetResult();
                    }
                    catch (AggregateException ae)
                    {
                        Trace.TraceError("There were one or more errors: ");

                        foreach (var exception in ae.InnerExceptions)
                        {
                            Trace.TraceError(exception.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("There was a problem when updating: " + ex);
                    }

                    return 0;
                }

                Trace.TraceError("Invalid command line: " + string.Join(" ", args));
                return 1;
            }

            var app = new App();
            app.DispatcherUnhandledException += AppOnDispatcherUnhandledException;
            app.Exit += ApplicationOnExit;
            app.Run();

            return 0;
        }

        static void AppOnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            if (Application.Current == null || Application.Current.MainWindow == null) return;

            if (args.Exception == null) return;

            MessageBox.Show(Application.Current.MainWindow, args.Exception.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        static void ApplicationOnExit(object sender, ExitEventArgs e)
        {

        }

        static void OnProgressChanged(object sender, UpdateProgress model)
        {
            if (model.Percent == -1)
            {
                Trace.TraceInformation(model.Message);
            }
            else
            {
                // Skip over every 15%
                if ((model.Percent % 15) == 0)
                {
                    Trace.TraceInformation($"{model.Percent} ({model.Percent}%)");
                }
            }
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (!(args.ExceptionObject is Exception ex)) return;

            if (Application.Current == null || Application.Current.MainWindow == null)
            {
                MessageBox.Show( ex.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(Application.Current.MainWindow, ex.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
