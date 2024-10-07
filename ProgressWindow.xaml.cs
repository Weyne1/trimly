using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VideoEditor
{
    public partial class ProgressWindow : Window
    {
        private MainWindow mainWindow;
        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void ChangeTitle(string title) => Title = title;

        public void ChangeVideoTitle(string videoTitle)
        {
            VideoTitle.Text = videoTitle;
            mainWindow.ChangeRenderText("[...%] " + videoTitle);
        }

        public async void UpdateProgress(double progress)
        {
            await Task.Run(() => Dispatcher.Invoke(() =>
            {
                double currentProgress = ProgressBar.Value;

                AnimateProgressBar(currentProgress, progress, TimeSpan.FromSeconds(0.5));
                AnimateText(currentProgress, progress, TimeSpan.FromSeconds(0.5));
            }));
        }


        private void AnimateProgressBar(double fromValue, double toValue, TimeSpan duration)
        {
            DoubleAnimation progressBarAnimation = new DoubleAnimation
            {
                From = fromValue,
                To = toValue,
                Duration = duration,
                EasingFunction = new QuadraticEase()
            };

            ProgressBar.BeginAnimation(ProgressBar.ValueProperty, progressBarAnimation);
        }

        private void AnimateText(double fromValue, double toValue, TimeSpan duration)
        {
            DoubleAnimation textAnimation = new DoubleAnimation
            {
                From = fromValue,
                To = toValue,
                Duration = duration,
                EasingFunction = new QuadraticEase()
            };

            textAnimation.CurrentTimeInvalidated += (s, e) =>
            {
                if (s is Clock clock)
                {
                    double currentProgress = (double)clock.CurrentTime.Value.Ticks / clock.NaturalDuration.TimeSpan.Ticks;
                    double currentValue = fromValue + (toValue - fromValue) * currentProgress;
                    Percent.Text = $"{currentValue:0.00}%";
                }
            };
            Percent.BeginAnimation(OpacityProperty, textAnimation);
        }



        public void SetWindowParent(MainWindow window) => mainWindow = window;
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mainWindow.EnableRenderButton();
            mainWindow.ChangeRenderText("[100%] " + VideoTitle.Text);

            Process[] ffmpegProcesses = Process.GetProcessesByName("ffmpeg");

            foreach (Process process in ffmpegProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();

                    ChangeTitle("Рендеринг [Отмена]");
                    mainWindow.StopRender();
                    mainWindow.ChangeRenderText("[отменён] " + VideoTitle.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при завершении процесса ffmpeg: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
