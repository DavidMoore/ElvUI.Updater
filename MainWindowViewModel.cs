using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SadRobot.ElvUI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {

        int progressValue = -1;
        string statusText;
        string buttonText = "OK";
        readonly Progress<UpdateProgress> progress;
        CancellationTokenSource cancellationToken;
        bool progressIsIndeterminate;

        public MainWindowViewModel()
        {
            ProgressMin = 0;
            ProgressMax = 100;
            StartCommand = new DelegateCommand(Start, IsStartEnabled);
            
            progress = new Progress<UpdateProgress>(ProgressHandler);
            cancellationToken = new CancellationTokenSource();
        }

        void Start(object state)
        {
            cancellationToken.Dispose();
            cancellationToken = new CancellationTokenSource();
            Task.Factory.StartNew(StartAsync, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        internal Task StartAsync()
        {
            return Updater.MainAsync(progress);
        }

        bool IsStartEnabled(object state)
        {

            return true;
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