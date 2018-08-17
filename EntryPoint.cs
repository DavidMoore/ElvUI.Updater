using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;

namespace SadRobot.ElvUI
{
    static class EntryPoint
    {
        [STAThread]
        internal static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            if (args.Length > 0)
            {
                if (args.Any(x => string.Equals(x, "/silent", StringComparison.OrdinalIgnoreCase)))
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
                }
                else
                {
                    Trace.TraceError("Invalid command line: " + string.Join(" ", args));
                }

                return;
            }

            new App().Run();
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
            if (Application.Current == null || Application.Current.MainWindow == null) return;

            var ex = args.ExceptionObject as Exception;
            if (ex == null) return;

            MessageBox.Show(Application.Current.MainWindow, ex.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
