using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using Microsoft.Win32;
using VideoEditor.Properties;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Path = System.IO.Path;
using Point = System.Windows.Point;

namespace VideoEditor
{
    public partial class MainWindow : Window
    {
        private ProgressWindow progressWindow;

        private bool isRendering = false;
        private bool isSnapshoting = false;
        private bool isSkipping = false;
        private bool renderShutdown = false;
        private bool isMediaPlaying = false;
        private bool isDragging = false;
        private bool isFullScreen = false;
        private bool hasFirstMarker = false;
        private bool hasSecondMarker = false;
        private bool mergeAudioTracks = true;
        private bool playAfterDragging = false;

        private short fpsBuffer = 0;
        private short sampleRatesBuffer = 0;
        private int videoBitrateBuffer = 0;
        private int audioBitrateBuffer = 0;
        private long globalBitrate = 0;
        private long globalAudioBitrate = 0;
        private double globalFPS = 0;
        private float audioVolume = 1.0f;
        private float videoSpeed = 1.0f;
        private float aspectRatio = 1.777f;
        private float frameDuration = 0;
        private double videoDuration = 0;
        private double firstMarkerValue = 0;
        private double secondMarkerValue = 0;
        private double zoomInPoint = 0;
        private double zoomOutPoint = 0;

        private string outputType = ".mp4";
        private readonly string version = "Pro 1.4.0";

        private WindowState previousWindowState;
        private WindowStyle previousWindowStyle;
        private ResizeMode previousResizeMode;

        public MainWindow()
        {
            InitializeComponent();
            ConfigureFFMpeg();
            LoadFromSettings();

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            timer.Tick += Timer_Tick;
            timer.Start();

            Title = "Fast Editor " + version;

            LastRenderedVideoText.Opacity = 0;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (hasFirstMarker || hasSecondMarker)
            {
                if (hasFirstMarker)
                {
                    DeleteMarker(1);
                }
                if (hasSecondMarker)
                {
                    DeleteMarker(2);
                }
            }

        }

        private void ConfigureFFMpeg()
        {

            var binaryFolder = AppDomain.CurrentDomain.BaseDirectory;

            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = binaryFolder,
            });
        }

        // Настройка горячих клавиш
        private async void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.I)
            {
                Point thumbPosition = GetThumbPosition(TrimSlider);

                // Если есть второй маркер на такой же позиции, удаление второго
                if (hasSecondMarker && ((int)secondMarkerValue == (int)TrimSlider.Value))
                {
                    if (hasFirstMarker)
                        DeleteMarker(1);

                    DeleteMarker(2);
                    await CreateMarker(thumbPosition, 1);
                }

                // Если есть второй маркер левее указанной позиции, удаление второго
                else if (hasSecondMarker && (secondMarkerValue < TrimSlider.Value))
                {
                    DeleteMarker(2);

                    if (hasFirstMarker)
                        DeleteMarker(1);

                    await CreateMarker(thumbPosition, 1);
                }

                // Если нет особых условий
                else
                {
                    if (hasFirstMarker)
                        DeleteMarker(1);
                    await CreateMarker(thumbPosition, 1);
                }

            }

            else if (e.Key == Key.O)
            {
                Point thumbPosition = GetThumbPosition(TrimSlider);

                // Если есть первый маркер на такой же позиции, удаление первого
                if (hasFirstMarker && ((int)firstMarkerValue == (int)TrimSlider.Value))
                {
                    if (hasSecondMarker)
                        DeleteMarker(2);

                    DeleteMarker(1);
                    await CreateMarker(thumbPosition, 2);
                }

                // Если есть первый маркер правее указанной позиции, удаление первого
                else if (hasFirstMarker && (firstMarkerValue > TrimSlider.Value))
                {
                    DeleteMarker(1);

                    if (hasSecondMarker)
                        DeleteMarker(2);

                    await CreateMarker(thumbPosition, 2);
                }

                // Если нет особых условий
                else
                {
                    if (hasSecondMarker)
                        DeleteMarker(2);
                    await CreateMarker(thumbPosition, 2);
                }
            }

            else if (e.Key == Key.Z) SetZoom();

            else if (e.Key == Key.X) ResetZoom();

            else if ((e.Key == Key.Space) || (e.Key == Key.K))
            {
                TrimSlider.Focus();
                ChangeMediaPlayerStatus();
            }

            else if ((e.Key == Key.Enter) && TrimSliderText.IsFocused) await EditTrimSliderText();

            else if (e.Key == Key.F11)
            {
                if (!isFullScreen)
                {
                    // Save current window state
                    previousWindowState = WindowState;
                    previousWindowStyle = WindowStyle;
                    previousResizeMode = ResizeMode;

                    // Switch to full screen
                    WindowState = WindowState.Maximized;
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.NoResize;

                    isFullScreen = true;
                }
                else
                {
                    // Restore previous window state
                    WindowState = previousWindowState;
                    WindowStyle = previousWindowStyle;
                    ResizeMode = previousResizeMode;

                    isFullScreen = false;
                }
            }
        }
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.J && mediaElement.HasVideo)
            {
                if (isSkipping) return;

                isSkipping = true;
                if (TrimSlider.Value <= 5000) 
                    TrimSlider.Value = 0;
                else TrimSlider.Value -= 5000;

                mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
                if (!isMediaPlaying && !isSnapshoting)
                {
                    await SetPreviewFrame(80);
                }
                await Task.Delay(100);
                isSkipping = false;
            }

            else if (e.Key == Key.L && mediaElement.HasVideo)
            {
                if (isSkipping) return;

                isSkipping = true;
                if (TrimSlider.Value >= TrimSlider.Maximum - 5000) 
                    TrimSlider.Value = TrimSlider.Maximum;
                else TrimSlider.Value += 5000;

                mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
                if (!isMediaPlaying && !isSnapshoting)
                {
                    await SetPreviewFrame(80);
                }
                await Task.Delay(100);
                isSkipping = false;
            }

            if ((e.Key == Key.Left || e.Key == Key.Right) && mediaElement.HasVideo && (TrimSlider.IsFocused || CursorRect.IsFocused))
            {
                if (isSkipping) return;

                isSkipping = true;

                double newValue = TrimSlider.Value;

                if (e.Key == Key.Left)
                    newValue = Math.Max(0, TrimSlider.Value - frameDuration);

                else if (e.Key == Key.Right)
                    newValue = Math.Min(TrimSlider.Maximum, TrimSlider.Value + frameDuration);

                TrimSlider.Value = newValue;
                mediaElement.Position = TimeSpan.FromMilliseconds(newValue);

                if (!isMediaPlaying && !isSnapshoting)
                {
                    await SetPreviewFrame(20, true);
                }
                else if (isMediaPlaying)
                {
                    ChangeMediaPlayerStatus();
                }

                await Task.Delay(50);
                isSkipping = false;
            }
        }

        private void MuteVideo()
        {
            mediaElement.Volume = 0;
        }
        private void UnmuteVideo()
        {
            mediaElement.Volume = audioVolume / 2;
        }

        private void WidthTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox) textBox.CaretIndex = textBox.Text.Length;

            if (int.TryParse(WidthInput.Text, out int wigth))
            {
                if (wigth == 0)
                    WidthInput.Text = "1";
                if (wigth > 7680) WidthInput.Text = "7680";
                if (LinkCheck != null && LinkCheck.IsChecked == true && wigth > 1)
                {
                    HeightInput.Text = ((int)(wigth / aspectRatio)).ToString();
                }
            }
            else WidthInput.Text = Regex.Replace(WidthInput.Text, "[^0-9]", "");
        }
        private void WidthTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(WidthInput.Text, out int wigth))
            {
                if (wigth < 150) WidthInput.Text = "150";
                else if (wigth % 2 != 0) WidthInput.Text = $"{wigth + 1}";

                if (LinkCheck.IsChecked != null && LinkCheck.IsChecked == true
                    && int.TryParse(HeightInput.Text, out int heigth))
                {
                    if (heigth % 2 != 0) HeightInput.Text = $"{heigth + 1}";
                }
            }
        }
        private void HeightTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox) textBox.CaretIndex = textBox.Text.Length;

            if (int.TryParse(HeightInput.Text, out int heigth))
            {
                if (HeightInput.Text.StartsWith("0"))
                    HeightInput.Text = HeightInput.Text.Substring(1);
                if (heigth > 4320) HeightInput.Text = "4320";
            }
            else HeightInput.Text = Regex.Replace(HeightInput.Text, "[^0-9]", "");
        }
        private void HeightTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(HeightInput.Text, out int heigth))
            {
                if (heigth % 2 != 0) HeightInput.Text = $"{heigth + 1}";
            }
        }
        private void LinkCheck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.IsChecked == true)
                HeightInput.IsEnabled = false;
            else
                HeightInput.IsEnabled = true;
        }
        
        
        private void SelectInputFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                InputFilePath.Text = openFileDialog.FileName;
                SetVideoDimensions(openFileDialog.FileName);
                LoadVideo(openFileDialog.FileName);
            }

            if (!string.IsNullOrEmpty(InputFilePath.Text) && !string.IsNullOrEmpty(OutputFilePath.Text) && !isRendering)
            {
                RenderButton.IsEnabled = true;
            }
        }
        private void SelectOutputFile_Click(object sender, RoutedEventArgs e)
        {
            string selectedOutputType = ((ComboBoxItem)TypeComboBox.SelectedItem).Content.ToString();
            string filename;
            string filter;

            switch (selectedOutputType)
            {
                case "MOV": filter = "MOV Files|*.mov"; break;
                case "AVI": filter = "AVI Files|*.avi"; break;
                case "MKV": filter = "MKV Files|*.mkv"; break;
                default: filter = "MP4 Files|*.mp4"; break;
            }

            if (!string.IsNullOrEmpty(OutputFilePath.Text))
                filename = Path.GetFileNameWithoutExtension(OutputFilePath.Text);
            else 
                filename = Path.GetFileNameWithoutExtension(InputFilePath.Text);

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = filename
        };
            if (saveFileDialog.ShowDialog() == true)
            {
                OutputFilePath.Text = saveFileDialog.FileName;
            }

            if (OutputFilePath.Text != string.Empty)
            {
                OutputFilePathText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC8C8C8"));
            }

            if (!string.IsNullOrEmpty(InputFilePath.Text) && !string.IsNullOrEmpty(OutputFilePath.Text) && !isRendering)
            {
                RenderButton.IsEnabled = true;
            }
        }

        private async void SetVideoDimensions(string inputPath)
        {
            try
            {
                ResetZoom();

                var videoInfo = FFProbe.Analyse(inputPath);

                videoDuration = videoInfo.Duration.TotalMilliseconds;
                zoomOutPoint = videoDuration;

                secondMarkerValue = videoDuration;
                TrimSlider.Maximum = videoDuration;
                TrimSlider.Value = TrimSlider.Minimum;

                globalBitrate = videoInfo.PrimaryVideoStream.BitRate;
                globalAudioBitrate = videoInfo.PrimaryAudioStream?.BitRate ?? 0;
                globalFPS = (short)videoInfo.PrimaryVideoStream.FrameRate;
                frameDuration = (short)(1000 / globalFPS);

                if (AutoParamCheck.IsChecked == true)
                {
                    WidthInput.Text = videoInfo.PrimaryVideoStream.Width.ToString();
                    HeightInput.Text = videoInfo.PrimaryVideoStream.Height.ToString();
                    FPSInput.Text = Math.Round(videoInfo.PrimaryVideoStream.FrameRate).ToString();
                    VideoBitrateInput.Text = ((int)globalBitrate / 1000).ToString();
                    AudioBitrateInput.Text = ((int)globalAudioBitrate / 1000).ToString();

                    if (videoInfo.PrimaryAudioStream != null && videoInfo.PrimaryAudioStream.SampleRateHz > 44150)
                        SampleRatesComboBox.SelectedIndex = 1;
                    else
                        SampleRatesComboBox.SelectedIndex = 0;
                }

                aspectRatio = Math.Abs(videoInfo.PrimaryVideoStream.Width / (float)videoInfo.PrimaryVideoStream.Height);

                await CalculateEstimatedSize();
                await ChangeDuration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении параметров видео: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            if (InputFilePath.Text != string.Empty)
            {
                InputFilePathText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC8C8C8"));
            }
        }

        private async void RenderButton_Click(object sender, RoutedEventArgs e) => await LoadToStartRender();
        private async Task LoadToStartRender()
        {
            if (InputFilePath.Text == OutputFilePath.Text)
            {
                string path = Path.GetDirectoryName(InputFilePath.Text);
                string newFilename = Path.GetFileNameWithoutExtension(InputFilePath.Text) + "_rendered" + outputType;
                OutputFilePath.Text = Path.Combine(path, newFilename);
            }

            if (!int.TryParse(FPSInput.Text, out int fps) ||
                !int.TryParse(VideoBitrateInput.Text, out int videoBitrate) ||
                !int.TryParse(AudioBitrateInput.Text, out int audioBitrate))
            {
                MessageBox.Show("Пожалуйста, введите корректные значения для FPS и битрейта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int width = int.Parse(WidthInput.Text);
            int height = int.Parse(HeightInput.Text);

            progressWindow = new ProgressWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner // Центрируем относительно главного окна
            };
            progressWindow.Show();

            try
            {
                isRendering = true;
                RenderButton.IsEnabled = false;

                TimeSpan trimStart = TimeSpan.FromMilliseconds(firstMarkerValue);
                TimeSpan trimEnd = TimeSpan.FromMilliseconds(secondMarkerValue);

                if (isMediaPlaying) ChangeMediaPlayerStatus();

                progressWindow.SetWindowParent(this);
                progressWindow.ChangeTitle("Рендеринг...");
                progressWindow.ChangeVideoTitle(Path.GetFileName(OutputFilePath.Text));

                await RenderAsync(InputFilePath.Text, OutputFilePath.Text, fps, videoBitrate, audioBitrate, width, height, trimStart, trimEnd);

                await Task.Delay(1000);
                isRendering = false;
                progressWindow.ChangeTitle("Рендеринг [Завершено]");
                progressWindow.Close();

                // Проверяем, существует ли уже файл с таким именем
                if (File.Exists(OutputFilePath.Text))
                {
                    string path = Path.GetDirectoryName(OutputFilePath.Text);
                    string fileName = Path.GetFileNameWithoutExtension(OutputFilePath.Text);
                    string extension = Path.GetExtension(OutputFilePath.Text);

                    int openBracketIndex = fileName.LastIndexOf(" (");
                    int closeBracketIndex = fileName.LastIndexOf(")");

                    if (openBracketIndex != -1 && closeBracketIndex != -1 && closeBracketIndex > openBracketIndex)
                    {
                        string numberString = fileName.Substring(openBracketIndex + 2, closeBracketIndex - openBracketIndex - 2);
                        if (int.TryParse(numberString, out int number))
                        {
                            fileName = fileName.Substring(0, openBracketIndex) + $" ({number + 1})";
                        }
                    }
                    else fileName += " (1)";

                    // Формируем новое имя файла с учетом пути и расширения
                    OutputFilePath.Text = Path.Combine(path, fileName + extension);
                }
            }
            catch (Exception ex)
            {
                if (!renderShutdown)
                {
                    MessageBox.Show($"Ошибка рендера видео: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressWindow.Close();
                    ChangeRenderText("[ошибка] " + Path.GetFileName(InputFilePath.Text));
                }
                isRendering = false;
                RenderButton.IsEnabled = true;
            }
        }
        private async Task RenderAsync(string inputPath, string outputPath, int fps, 
            int videoBitrate, int audioBitrate, int width, int height, TimeSpan trimStart, TimeSpan trimEnd)
        {
            var videoInfo = await FFProbe.AnalyseAsync(inputPath);
            var totalTime = videoInfo.Duration;
            var outputDuration = trimEnd - trimStart;
            int audioStreamCount = videoInfo.AudioStreams.Count();
            byte selectedCodec = (byte)CodecComboBox.SelectedIndex;
            byte selectedSampleRates = (byte)SampleRatesComboBox.SelectedIndex;

            // Рассчет продолжительности с учетом скорости
            var adjustedDuration = TimeSpan.FromTicks((long)(outputDuration.Ticks / videoSpeed));

            // Определение видеокодека
            string videoCodec;
            switch (selectedCodec)
            {
                case 1: videoCodec = "h264_nvenc"; break;
                case 2: videoCodec = "h264_qsv"; break;
                case 3: videoCodec = "h264_amf"; break;
                default: videoCodec = "libx264"; break;
            }

            // Определение частоты дискретизации
            int sampleRates;
            switch (selectedSampleRates)
            {
                case 1: sampleRates = 48000; break;
                default: sampleRates = 41000; break;
            }


            await Task.Run(() =>
            {
                // Основная логика конвертации
                var arguments = FFMpegArguments
                    .FromFileInput(inputPath, true, options => options.Seek(trimStart))
                    .OutputToFile(outputPath, true, options =>
                    {
                        options
                            .WithVideoCodec(videoCodec)
                            .WithFramerate(fps)
                            .Resize(width, height)
                            .WithVideoBitrate(videoBitrate)
                            .UsingMultithreading(true)
                            .WithAudioBitrate(audioBitrate)
                            .WithDuration(adjustedDuration);

                        // Настройка аудио
                        if (audioVolume == 0)
                        {
                            options.WithCustomArgument("-an"); // Удаление звука
                        }
                        else
                        {
                            if (audioStreamCount > 1) // Для нескольких дорожек
                            {
                                string filterComplex = string.Empty;

                                for (int i = 0; i < audioStreamCount; i++) // Установка громкости и скорости для каждой дорожки
                                {
                                    filterComplex += $"[0:a:{i}]volume={audioVolume.ToString().Replace(',', '.')}," +
                                    $"atempo={videoSpeed.ToString().Replace(',', '.')}," +
                                    $"aformat=sample_fmts=s16:sample_rates={sampleRates}:channel_layouts=stereo[a{i}];";
                                }

                                if (mergeAudioTracks) // Объединение в одну дорожку
                                {
                                    filterComplex += string.Join("", Enumerable.Range(0, audioStreamCount).Select(i => $"[a{i}]"));
                                    filterComplex += $"amerge=inputs={audioStreamCount},pan=stereo|c0<c0+c2|c1<c1+c3[a]";
                                    options.WithCustomArgument($"-filter_complex \"{filterComplex}\" -map 0:v -map [a]");
                                }
                                else // Рендер без объединения дорожек
                                {
                                    filterComplex = filterComplex.TrimEnd(';');
                                    options.WithCustomArgument($"-filter_complex \"{filterComplex}\"");

                                    for (int i = 0; i < audioStreamCount; i++)
                                    {
                                        options.WithCustomArgument($"-map [a{i}]");
                                    }

                                    options.WithCustomArgument($"-map 0:v");
                                }
                            }
                            else // Для одной дорожки
                            {
                                string audioFilter = $"volume={audioVolume.ToString().Replace(',', '.')}," +
                                $"atempo={videoSpeed.ToString().Replace(',', '.')}," +
                                $"aformat=sample_fmts=s16:sample_rates={sampleRates}:channel_layouts=stereo";
                                options.WithCustomArgument($"-filter:a \"{audioFilter}\"");
                            }
                        }

                        // Настройка скорости видео
                        if (videoSpeed != 1.0f)
                        {
                            options.WithCustomArgument($"-filter:v \"setpts={(1 / videoSpeed).ToString().Replace(',', '.')}*PTS\"");
                        }
                    })
                    .NotifyOnProgress(progress =>
                    {
                        progressWindow.UpdateProgress(progress);
                    }, totalTime)
                    .ProcessSynchronously();
            });

        }

        public void EnableRenderButton()
        {
            RenderButton.IsEnabled = true;
        }
        public void ChangeRenderText(string text)
        {
            LastRenderedVideoText.IsEnabled = true;
            LastRenderedVideoText.Opacity = 1;
            LastRenderedVideoText.Text = text;
        }
        public void StopRender()
        {
            renderShutdown = true;
        }

        private void FPSInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            FPSInput.Text = Regex.Replace(FPSInput.Text, "[^0-9]", "");
        }
        private async void VideoBitrateInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            VideoBitrateInput.Text = Regex.Replace(VideoBitrateInput.Text, "[^0-9]", "");
            await CalculateEstimatedSize();
        }
        private async void AudioBitrateInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            AudioBitrateInput.Text = Regex.Replace(AudioBitrateInput.Text, "[^0-9]", "");
            await CalculateEstimatedSize();
        }
        private void FPSInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FPSInput.Text) || FPSInput.Text == "0") FPSInput.Text = "1";
        }
        private void VideoBitrateInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (VideoBitrateInput.Text == string.Empty) VideoBitrateInput.Text = "0";
        }
        private void AudioBitrateInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AudioBitrateInput.Text == string.Empty) AudioBitrateInput.Text = "1";
        }


        private void VideoDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    string fileExtension = Path.GetExtension(filePath).ToLower();
                    string[] allowedExtensions = { ".mp4", ".mkv", ".avi", ".mov" };

                    if (allowedExtensions.Contains(fileExtension))
                    {
                        InputFilePath.Text = filePath;
                        SetVideoDimensions(filePath);
                        LoadVideo(filePath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(InputFilePath.Text) && !string.IsNullOrEmpty(OutputFilePath.Text) && !isRendering)
            {
                RenderButton.IsEnabled = true;
            }
        }
        private void ViewRenderedVideo(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (LastRenderedVideoText.Opacity != 0)
                {
                    string directoryPath = Path.GetDirectoryName(OutputFilePath.Text);

                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", directoryPath);
                    }
                    else
                    {
                        MessageBox.Show("Указанной папки не существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть путь: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task CalculateEstimatedSize()
        {
            try
            {
                if (!string.IsNullOrEmpty(InputFilePath.Text) &&
                    !string.IsNullOrEmpty(VideoBitrateInput.Text) &&
                    !string.IsNullOrEmpty(AudioBitrateInput.Text))
                {
                    int bitrate = ParseBitrate(VideoBitrateInput.Text, globalBitrate);
                    int audioBitrate = ParseBitrate(AudioBitrateInput.Text, globalAudioBitrate);

                    var duration = TimeSpan.FromMilliseconds(secondMarkerValue - firstMarkerValue);
                    var adjustedDuration = TimeSpan.FromTicks((long)(duration.Ticks / videoSpeed));
                    int newDuration = (int)adjustedDuration.TotalSeconds;

                    long sizeInBytes = (bitrate + (long)audioBitrate) * newDuration;
                    double sizeInMegabytes = (double)sizeInBytes / (8 * 1024 * 1024) * 1000;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        EstimatedSizeText.Text = $"≈{sizeInMegabytes:F2} МБ";
                    });
                }
            }
            catch (Exception ex)
            {
                EstimatedSizeText.Text = "- МБ";
                MessageBox.Show($"Не удалось рассчитать примерный размер видео: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private int ParseBitrate(string bitrateInput, long globalBitrate)
        {
            return int.TryParse(bitrateInput, out int bitrate) && bitrate > 0 ? bitrate : (int)globalBitrate / 1000;
        }

        private async Task ChangeDuration()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Расчет продолжительности с учетом скорости видео
                    var originalDuration = TimeSpan.FromMilliseconds(secondMarkerValue - firstMarkerValue);
                    var adjustedDuration = TimeSpan.FromTicks((long)(originalDuration.Ticks / videoSpeed));
                    var formattedDuration = adjustedDuration.ToString(@"h\:mm\:ss");

                    // Обновление UI
                    Dispatcher.Invoke(() => DurationText.Text = $"Итоговая длительность: {formattedDuration}");
                });
            }
            catch //(Exception ex)
            {
                Dispatcher.Invoke(() => DurationText.Text = "Ошибка расчета длительности");
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedOutputType = ((ComboBoxItem)TypeComboBox.SelectedItem).Content.ToString();

            string fileType;
            switch (selectedOutputType)
            {
                case "MOV": fileType = ".mov"; break;
                case "AVI": fileType = ".avi"; break;
                case "MKV": fileType = ".mkv"; break;
                default: fileType = ".mp4"; break;
            }

            outputType = fileType;

            if (!string.IsNullOrEmpty(OutputFilePath.Text))
            {
                string fileName = Path.GetFileNameWithoutExtension(OutputFilePath.Text) + outputType;
                OutputFilePath.Text = Path.GetDirectoryName(OutputFilePath.Text) + "\\" + fileName;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isDragging && mediaElement.Source != null && mediaElement.NaturalDuration.HasTimeSpan && isMediaPlaying)
            {
                TrimSlider.Value = mediaElement.Position.TotalMilliseconds;
            }
        }

        private void MediaElement_MouseDown(object sender, MouseButtonEventArgs e) => ChangeMediaPlayerStatus();

        private async void LoadVideo(string videoPath)
        {
            isMediaPlaying = false;

            mediaElement.Stop();
            mediaElement.Close();
            mediaElement.Source = null;
            MuteVideo();

            mediaElement.Source = new Uri($"{videoPath}", UriKind.RelativeOrAbsolute);

            DragAndDropImage.Opacity = 0;
            PlayImage.Opacity = 1;
            CursorRect.Cursor = Cursors.Hand;
            
            await SetVideoPrewiew();

            UnmuteVideo();
        }
        private async Task SetVideoPrewiew()
        {
            MuteVideo();
            mediaElement.Play();
            await Task.Delay(50);
            mediaElement.Stop();
            UnmuteVideo();
        }
        private void ChangeMediaPlayerStatus()
        {
            if (isMediaPlaying)
            {
                mediaElement.Pause();
                isMediaPlaying = false;
                PlayImage.Opacity = 1;
            }
            else if (!isMediaPlaying && InputFilePath.Text != string.Empty && !isRendering)
            {
                mediaElement.Play();
                isMediaPlaying = true;
                PlayImage.Opacity = 0;
            }
        }
        private Point GetThumbPosition(Slider slider)
        {
            // Get the Slider's template
            if (slider.Template.FindName("Thumb", slider) is UIElement thumb)
            {
                // Get the position of the Thumb relative to the window
                Point relativePoint = thumb.TransformToAncestor(TrimSlider).Transform(new Point(0, 0));
                return relativePoint;
            }
            return new Point(0, 0);
        }
        private async Task CreateMarker(Point position, ushort markerNumber)
        {
            ushort delta = 28; //Отступ для элемента

            var marker = new System.Windows.Controls.Image
            {
                Width = 25,
                Height = 25,
                Stretch = Stretch.Fill,
                Cursor = Cursors.Hand,
                Margin = new Thickness(position.X + delta - 25, 0, 0, 0)
            };

            if (markerNumber == 1)
            {
                firstMarkerValue = TrimSlider.Value;

                marker.Name = "FirstMarker";
                marker.Source = new BitmapImage(new Uri("pack://application:,,,/images/arrow_right.png"));
                marker.MouseLeftButtonDown += new MouseButtonEventHandler(FirstMarker_OnMouseLeftButtonDown);
                
                FirstMarkerPanel.Children.Add(marker);
                hasFirstMarker = true;
            }

            else if (markerNumber == 2)
            {
                secondMarkerValue = TrimSlider.Value;

                marker.Name = "SecondMarker";
                marker.Source = new BitmapImage(new Uri("pack://application:,,,/images/arrow_left.png"));
                marker.MouseLeftButtonDown += new MouseButtonEventHandler(SecondMarker_OnMouseLeftButtonDown);
                
                SecondMarkerPanel.Children.Add(marker);
                hasSecondMarker = true;
            }

            // Включает кнопку для зума
            if (hasFirstMarker && hasSecondMarker)
            {
                ZoomButton.IsEnabled = true;
            }

            await CalculateEstimatedSize();
            await ChangeDuration();
        }
        private async void DeleteMarker(ushort number)
        {
            if (number == 1 && hasFirstMarker)
            {
                for (int i = 0; i < FirstMarkerPanel.Children.Count; i++)
                    FirstMarkerPanel.Children.Remove(FirstMarkerPanel.Children[i]);
                hasFirstMarker = false;
                firstMarkerValue = 0;
            }
            if (number == 2 && hasSecondMarker)
            {
                for (int i = 0; i < SecondMarkerPanel.Children.Count; i++)
                    SecondMarkerPanel.Children.Remove(SecondMarkerPanel.Children[i]);
                hasSecondMarker = false;
                secondMarkerValue = videoDuration;
            }

            // Выключает кнопку для зума
            if (!hasFirstMarker || !hasSecondMarker)
            {
                ZoomButton.IsEnabled = false;
            }

            await ChangeDuration();
        }
        private void FirstMarker_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TrimSlider.Value = firstMarkerValue;
            mediaElement.Position = TimeSpan.FromMilliseconds(firstMarkerValue);
        }
        private void SecondMarker_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isMediaPlaying)
            {
                ChangeMediaPlayerStatus();
            }
            TrimSlider.Value = secondMarkerValue;
            mediaElement.Position = TimeSpan.FromMilliseconds(secondMarkerValue);
        }


        private async void TrimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider;

            if (!isDragging && isMediaPlaying)
            {
                const short tolerance = 50; // допустимое отклонение в миллисекундах

                if (Math.Abs(slider.Value - secondMarkerValue) <= tolerance)
                {
                    slider.Value = firstMarkerValue;
                    mediaElement.Position = TimeSpan.FromMilliseconds(firstMarkerValue);
                }
                else if (Math.Abs(slider.Value - zoomOutPoint) <= tolerance)
                {
                    slider.Value = hasFirstMarker ? firstMarkerValue : zoomInPoint;
                    mediaElement.Position = TimeSpan.FromMilliseconds(slider.Value);
                }
            }

            // Отображение времени рядом со слайдером в формате hh:mm:ss:ff
            TimeSpan time = TimeSpan.FromMilliseconds(slider.Value);
            short frames = (short)(slider.Value % 1000 / frameDuration);

            TrimSliderText.Text = $"{time:h\\:mm\\:ss\\:}{frames:D2}";

            if (!isSnapshoting && isDragging)
            {
                await SetPreviewFrame(100);
            }
        }

        private void TrimSlider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;

            if (sender is Slider slider)
            {
                var position = e.GetPosition(slider);
                double relativePosition = position.X / slider.ActualWidth;
                double newValue = relativePosition * (slider.Maximum - slider.Minimum) + slider.Minimum;
                slider.Value = newValue;
            }
        }
        private async void TrimSlider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);

            if (playAfterDragging)
            {
                await Task.Delay(150);
                ChangeMediaPlayerStatus();
                playAfterDragging = false;
            }
        }
        private async Task SetPreviewFrame(short sleepTime, bool muteVideo = false)
        {
            if (isSnapshoting) return;

            isSnapshoting = true;
            mediaElement.IsMuted = muteVideo;

            if (isMediaPlaying)
            {
                ChangeMediaPlayerStatus();
                playAfterDragging = true;
            }
            
            mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
            mediaElement.Play();

            await Task.Delay(sleepTime);

            mediaElement.Pause();

            mediaElement.IsMuted = false;
            isSnapshoting = false;
        }


        private void TrimSliderText_TextChanged(object sender, TextChangedEventArgs e)
        {
            TrimSliderText.Text = Regex.Replace(TrimSliderText.Text, "[^0-9:]", "");
        }

        private async void TrimSliderText_LostFocus(object sender, RoutedEventArgs e) => await EditTrimSliderText();
        private async Task EditTrimSliderText()
        {
            await Task.Run(() => Dispatcher.Invoke(async () =>
            {
                string[] parts = TrimSliderText.Text.Split(':');

                try
                {
                    ushort hours = ushort.Parse(parts[0]);
                    ushort minutes = ushort.Parse(parts[1]);
                    ushort seconds = ushort.Parse(parts[2]);
                    ushort frames = ushort.Parse(parts[3]);

                    // Преобразование кадров в миллисекунды
                    double millisecondsFromFrames = frames * frameDuration;

                    TrimSlider.Value = hours * 3600000 + minutes * 60000 + seconds * 1000 + millisecondsFromFrames;

                    TimeSpan value = TimeSpan.FromMilliseconds(TrimSlider.Value);
                    mediaElement.Position = value;
                    if (isSnapshoting) await SetPreviewFrame(50);
                    mediaElement.Position = value;
                }
                catch { }
            }));
        }


        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsInitialized)
            {
                VolumeText.Text = ((ushort)VolumeSlider.Value).ToString() + "%";
                audioVolume = (float)VolumeSlider.Value / 100;
                mediaElement.Volume = audioVolume / 2;
            }
        }
        private async void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsInitialized)
            {
                SpeedText.Text = Math.Round(SpeedSlider.Value, 1).ToString().Replace(',', '.') + "x";
                videoSpeed = (float)Math.Round(SpeedSlider.Value, 1);
                mediaElement.SpeedRatio = videoSpeed;

                await ChangeDuration();
                await CalculateEstimatedSize();
            }
        }

        private void ZoomButton_Click(object sender, RoutedEventArgs e)
        {
            SetZoom();
        }
        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
        }
        private void SetZoom()
        {
            if (hasFirstMarker && hasSecondMarker)
            {
                zoomInPoint = firstMarkerValue;
                zoomOutPoint = secondMarkerValue;

                TrimSlider.Value = zoomInPoint;
                TrimSlider.Minimum = zoomInPoint;
                TrimSlider.Maximum = zoomOutPoint;

                DeleteMarker(1);
                DeleteMarker(2);

                string startPoint = TimeSpan.FromMilliseconds(zoomInPoint).ToString(@"h\:mm\:ss");
                string endPoint = TimeSpan.FromMilliseconds(zoomOutPoint).ToString(@"h\:mm\:ss");

                ZoomStatusText.Text = $"Зум от {startPoint} до {endPoint}.";
                ResetZoomButton.IsEnabled = true;
            }
        }
        private void ResetZoom()
        {
            zoomInPoint = 0;
            zoomOutPoint = videoDuration;

            TrimSlider.Minimum = 0;
            TrimSlider.Maximum = videoDuration;

            DeleteMarker(1);
            DeleteMarker(2);

            ZoomStatusText.Text = "Предпросмотр без зума.";
            ResetZoomButton.IsEnabled = false;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            videoBitrateBuffer = int.Parse(VideoBitrateInput.Text.ToString());
            audioBitrateBuffer = int.Parse(AudioBitrateInput.Text.ToString());
            fpsBuffer = short.Parse(FPSInput.Text.ToString());
            sampleRatesBuffer = (short)SampleRatesComboBox.SelectedIndex;
        }
        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (videoBitrateBuffer > 0) 
                VideoBitrateInput.Text = videoBitrateBuffer.ToString();
            if (audioBitrateBuffer > 0) 
                AudioBitrateInput.Text = audioBitrateBuffer.ToString();
            if (fpsBuffer > 0)
                FPSInput.Text = fpsBuffer.ToString();

            SampleRatesComboBox.SelectedIndex = sampleRatesBuffer;
        }

        private void MergeAudioTracksCheck_Checked(object sender, RoutedEventArgs e)
        {
            mergeAudioTracks = (bool)MergeAudioTracksCheck.IsChecked;
        }

        private void LoadFromSettings()
        {
            var settings = AppSettings.Load();

            // Комбо боксы
            CodecComboBox.SelectedIndex = settings.Codec;
            TypeComboBox.SelectedIndex = settings.Type;
            SampleRatesComboBox.SelectedIndex = settings.SampleRates;

            // Галочки
            MergeAudioTracksCheck.IsChecked = settings.MergeAudioTracks;
            AutoParamCheck.IsChecked = settings.AutoMetaSet;


            // Выходной путь
            if (!string.IsNullOrEmpty(settings.Path))
            {
                OutputFilePath.Text = settings.Path;
                OutputFilePathText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC8C8C8"));
            }

            // Параметры видео
            if (settings.Width > 0) 
                WidthInput.Text = settings.Width.ToString();
            if (settings.Height > 0) 
                HeightInput.Text = settings.Height.ToString();
            if (settings.FPS > 0 && settings.FPS.ToString().Length < 4) 
                FPSInput.Text = settings.FPS.ToString();
            if (settings.VideoBitrate > 0 && settings.VideoBitrate.ToString().Length < 7) 
                VideoBitrateInput.Text = settings.VideoBitrate.ToString();
            if (settings.AudioBitrate > 0 && settings.AudioBitrate.ToString().Length < 4) 
                AudioBitrateInput.Text = settings.AudioBitrate.ToString();
            
            // Пресеты
            if (settings.BufferFPS > 0 && settings.BufferFPS.ToString().Length < 4) 
                fpsBuffer = settings.BufferFPS;
            if (settings.BufferVideoBitrate > 0 && settings.BufferVideoBitrate.ToString().Length < 7) 
                videoBitrateBuffer = settings.BufferVideoBitrate;
            if (settings.BufferAudioBitrate > 0 && settings.BufferAudioBitrate.ToString().Length < 4) 
                audioBitrateBuffer = settings.BufferAudioBitrate;

            // Положение окна
            if (settings.WindowWidth != 0 && settings.WindowHeight != 0)
            {
                Width = settings.WindowWidth;
                Height = settings.WindowHeight;
            }

            settings.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var settings = AppSettings.Load();

            settings.Path = OutputFilePath.Text;
            settings.Codec = (byte)CodecComboBox.SelectedIndex;
            settings.Type = (byte)TypeComboBox.SelectedIndex;
            settings.SampleRates = (byte)CodecComboBox.SelectedIndex;

            settings.MergeAudioTracks = (bool)MergeAudioTracksCheck.IsChecked;
            settings.AutoMetaSet = (bool)AutoParamCheck.IsChecked;

            settings.Width = int.Parse(WidthInput.Text);
            settings.Height = int.Parse(HeightInput.Text);
            settings.FPS = short.Parse(FPSInput.Text);
            settings.VideoBitrate = int.Parse(VideoBitrateInput.Text);
            settings.AudioBitrate = int.Parse(AudioBitrateInput.Text);

            settings.BufferFPS = fpsBuffer;
            settings.BufferVideoBitrate = videoBitrateBuffer;
            settings.BufferAudioBitrate = audioBitrateBuffer;
            settings.BufferSampleRates = sampleRatesBuffer;

            if (!isFullScreen)
            {
                settings.WindowWidth = (short)Width;
                settings.WindowHeight = (short)Height;
            }

            settings.Save();
        }
    }
}