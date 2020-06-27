using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SadRobot.ElvUI.Deployment;

namespace SadRobot.ElvUI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        int progressValue = -1;
        string statusText;
        string buttonText = "Update";

        readonly Progress<UpdateProgress> progress;
        CancellationTokenSource cancellationToken;
        bool progressIsIndeterminate;
        ProgressState state;
        readonly Dispatcher dispatcher;

        public MainWindowViewModel()
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ProgressMin = 0;
            ProgressMax = 100;
            StartCommand = new DelegateCommand(Start, IsStartEnabled);
            State = ProgressState.Ready;
            progress = new Progress<UpdateProgress>(ProgressHandler);
            cancellationToken = new CancellationTokenSource();
        }

        public ProgressState State
        {
            get => state;
            set
            {
                if (state == value) return;
                state = value;
                OnPropertyChanged();
                StartCommand.OnCanExecuteChanged();
            }
        }

        void Start(object state)
        {
            if (State == ProgressState.Updating) return;

            State = ProgressState.Updating;
            cancellationToken.Dispose();
            cancellationToken = new CancellationTokenSource();
            Task.Factory.StartNew(StartAsync, cancellationToken.Token, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        internal async Task StartAsync()
        {
            var updater = new ApplicationUpdater();
            var args = new UpdateCheckArgs
            {
                Uri = "https://api.github.com/repos/DavidMoore/ElvUI.Updater/releases",
                AssetName = "elvui.exe",
                AllowPreRelease = true
            };

            var update = await updater.CheckForUpdateAsync(args);

            if (update != null)// && update.IsUpgrade())
            {
                if (MessageBoxHelper.Show(dispatcher, "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes,
                    "An update to version {0} is available. Would you like to update now?", update.Version) == MessageBoxResult.Yes)
                {
                    var pm = new Progress<ProgressModel>(model => progress.Report(model.Caption, model.Value));

                    var result = await updater.DownloadAsync(update, pm);

                    if (result != null)
                    {
                        // To overwrite self with new file, we need to re-launch
                        var launchArgs = new LaunchArgs
                        {
                            Target = result.Filename,
                            Args = " /launch"
                        };

                        await updater.LaunchAsync(launchArgs);
                        Application.Current.Shutdown(0);
                        return;
                    }
                }
            }
            
            await Updater.MainAsync(progress);
        }

        bool IsStartEnabled(object state)
        {
            switch (State)
            {
                case ProgressState.Ready:
                case ProgressState.Error:
                case ProgressState.Done:
                    return true;
                case ProgressState.Updating:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void ProgressHandler(UpdateProgress model)
        {
            if (model == null) return;

            StatusText = model.Message;

            if (model.Percent <= 0)
            {
                ProgressIsIndeterminate = true;
            }
            else
            {
                ProgressValue = model.Percent;
                ProgressIsIndeterminate = false;
            }

            if (model.Percent == 100)
            {
                State = model.Exception != null ? ProgressState.Done : ProgressState.Error;
            }
            else
            {
                State = ProgressState.Updating;
            }
        }

        public DelegateCommand StartCommand { get; set; }

        public bool ProgressIsIndeterminate
        {
            get => progressIsIndeterminate;
            set
            {
                if (value == progressIsIndeterminate) return;
                progressIsIndeterminate = value;
                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get => progressValue;
            set
            {
                if (value == progressValue) return;
                progressValue = value;
                ProgressIsIndeterminate = value < 0;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => statusText;
            set
            {
                if (value == statusText) return;
                statusText = value;
                OnPropertyChanged();
            }
        }

        public string ButtonText
        {
            get => buttonText;
            set
            {
                if (value == buttonText) return;
                buttonText = value;
                OnPropertyChanged();
            }
        }

        public int ProgressMin { get; set; }

        public int ProgressMax { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}