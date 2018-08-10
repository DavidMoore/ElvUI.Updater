using System;

namespace SadRobot.ElvUI
{
    class UpdateProgress
    {
        public const int Indeterminite = -1;

        public UpdateProgress(string message, int percent) : this(message)
        {
            Message = message;
            Percent = percent;
        }

        public UpdateProgress(string message)
        {
            Message = message;
        }
        
        public UpdateProgress(string message, int percent, double downloaded, double length) : this(message, percent)
        {
            Downloaded = downloaded;
            Length = length;
        }

        public double Downloaded { get; set; }
        public double Length { get; set; }

        public int Percent { get; set; } = -1;

        public string Message { get;set; }
    }

    static class ProgressExtensions
    {
        public static void Report(this IProgress<UpdateProgress> progress, string message, int percent)
        {
            progress.Report(new UpdateProgress(message, percent));
        }
        public static void Report(this IProgress<UpdateProgress> progress, string message, int percent, double downloaded, double total)
        {
            progress.Report(new UpdateProgress(  string.Format(message, downloaded, total), percent, downloaded, total));
        }

        public static void Report(this IProgress<UpdateProgress> progress, string message)
        {
            progress.Report(new UpdateProgress(message, UpdateProgress.Indeterminite));
        }
    }
}