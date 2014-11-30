using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ExifLib;
using log4net;
using log4net.Config;

namespace VideoScreensaver {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        // Logger
        private static readonly ILog logger =
           LogManager.GetLogger(typeof(MainWindow));

        private bool preview;

        private VideoList videoList;

        private Point? lastMousePosition = null;  // Workaround for "MouseMove always fires when maximized" bug.

        private int currentMediaIndex = 0;

        private double volume {
            get { return FullScreenMedia.Volume; }
            set {
                FullScreenMedia.Volume = Math.Max(Math.Min(value, 1), 0);
                PreferenceManager.WriteVolumeSetting(FullScreenMedia.Volume);
            }
        }

        private string currentMediaPath;
        private DateTime currentMediaDateTaken;
        private string currentMediaTitle;

        // Force logging of a string regardless of level setting configured in Log4Net
        // See http://stackoverflow.com/questions/4229194/usage-of-log4net-to-always-log-a-value
        private void ForceLog(string s)
        {
            // Temporarily Reset the level to ALL
            log4net.ILog log = logger;
            log4net.Repository.Hierarchy.Logger l = (log4net.Repository.Hierarchy.Logger)log.Logger;
            var oldLevel = l.Level;
            l.Level = l.Hierarchy.LevelMap["ALL"];

            // Output message to log
            logger.Info(s);

            // Reset back to old logging level
            l.Level = oldLevel;
        }

        public MainWindow(bool preview) {
            // Initialize Log4Net Logger
            XmlConfigurator.Configure();
            ForceLog("Start Session");

            videoList = new VideoList();        // UNDONE - restore from persisted storage if present

            InitializeComponent();
            this.preview = preview;
            FullScreenMedia.Volume = PreferenceManager.ReadVolumeSetting();
            if (preview) {
                ShowError("When fullscreen, control volume with up/down arrows or mouse wheel.");
            }
        }

        private void ScrKeyDown(object sender, KeyEventArgs e) {
            logger.DebugFormat("ScrKeyDown - {0}", e.Key.ToString());
            switch (e.Key)
            {
                case Key.Right:
                    NextMediaItem();
                    break;
                case Key.Left:
                    PreviousMediaItem();
                    break;
                case Key.Up:
                case Key.VolumeUp:
                    volume += 0.1;
                    break;
                case Key.Down:
                case Key.VolumeDown:
                    volume -= 0.1;
                    break;
                case Key.VolumeMute:
                case Key.D0:
                    volume = 0;
                    break;
                case Key.I:     // Show information about current photo
                    ShowInfo();
                    break;

                default:
                    EndFullScreensaver();
                    break;
            }
        }

        private void ShowInfo()
        {
            PropertiesControl infoControl = new PropertiesControl();

            infoControl.PhotoTitle = currentMediaTitle;
            infoControl.PhotoTimeTaken = currentMediaDateTaken;
            infoControl.PhotoFilePath = currentMediaPath;

            Window propertiesWindow = new Window()
            {
                WindowStyle = System.Windows.WindowStyle.None,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                Content = infoControl,
                Height = infoControl.Height,
                Width = infoControl.Width,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Owner = this
            };

            propertiesWindow.ShowDialog();
        }

        private void ScrMouseWheel(object sender, MouseWheelEventArgs e) {
            volume += e.Delta / 1000.0;
        }

        private void ScrMouseMove(object sender, MouseEventArgs e) {
            // Workaround for bug in WPF.
            Point mousePosition = e.GetPosition(this);
            if (lastMousePosition != null && mousePosition != lastMousePosition) {
                EndFullScreensaver();
            }
            lastMousePosition = mousePosition;
        }

        private void ScrMouseDown(object sender, MouseButtonEventArgs e) {
            EndFullScreensaver();
        }

        private void ScrSizeChange(object sender, SizeChangedEventArgs e) {
            FullScreenMedia.Width = e.NewSize.Width;
            FullScreenMedia.Height = e.NewSize.Height;
        }

        // End the screensaver only if running in full screen. No-op in preview mode.
        private void EndFullScreensaver() {
            ForceLog("End Session");

            if (!preview) {
                Close();
            }
        }

        private void GetAllVideos(String videoPath)
        {
            logger.Debug("GetAllVideos");

            try
            {
                IEnumerable<String> files = Directory.EnumerateFiles(videoPath, "*", SearchOption.AllDirectories);
                            
                foreach (var f in files)
                {
                    if (f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        videoList.Add(f);
                        logger.DebugFormat("Adding file - {0}", f.ToString());
                    }
                }
                
                logger.InfoFormat("{0} files found.", videoList.Count);
            }
            catch (UnauthorizedAccessException UAEx)
            {
                logger.Error("Retrieving files", UAEx);
            }
            catch (PathTooLongException PathEx)
            {
                logger.Error("Path Too Long", PathEx);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            String videoPath = PreferenceManager.ReadVideoSettings();

            if (videoPath.Length == 0) {
                // Default to Pictures
                videoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
            
            if (!Directory.Exists(videoPath)) {
                ShowError("This screensaver needs to be configured before anthing is displayed.");
            }
            else
            {
                // Is there a searilized list?
                if (File.Exists("ScreenSaver.data.bin"))
                {
                    logger.Info("Cached pictures list exists");
                    try
	                {
			            using (Stream stream = File.Open("ScreenSaver.data.bin", FileMode.Open))
			            {
			                BinaryFormatter bin = new BinaryFormatter();

			                videoList = (VideoList)bin.Deserialize(stream);
		                }
                    }
		            catch (IOException ex)
		            {
                        logger.Error("Deserializing video list", ex);
		            }
                    logger.InfoFormat("Read {0} picture paths from cached file", videoList.Count);
                }
                else 
                {
                    logger.Info("No cached pictures list exists - enumerating files");

                    GetAllVideos(videoPath);

                    // Serialize it to disk
                    try
                    {
                        logger.Info("Writing enumerated paths to disk cache");
                        using (Stream stream = File.Open("ScreenSaver.data.bin", FileMode.Create))
                        {
                            BinaryFormatter bin = new BinaryFormatter();
                            bin.Serialize(stream, videoList);
                        }
                    }
                    catch (IOException ex)
                    {
                        logger.Error("Serializing video list", ex);
                    }
                }

                // Randomly reoder the list
                videoList = (VideoList) videoList.ShuffleFilePaths();

                GeneralData.Text = "File count: " + videoList.Count;
                logger.InfoFormat("Starting screen show at {0}", (String)videoList[currentMediaIndex]);
                SetNewMedia((String)videoList[currentMediaIndex]);
            }
        }

        private void ShowError(string errorMessage) {
            logger.ErrorFormat("ShowError - {0}", errorMessage);
            ErrorText.Text = errorMessage;
            ErrorText.Visibility = System.Windows.Visibility.Visible;
            if (preview) {
                ErrorText.FontSize = 12;
            }
        }

        private void MediaEnded(object sender, RoutedEventArgs e) {
            NextMediaItem();
            //FullScreenMedia.Position = new TimeSpan(0);
        }

        private void NextMediaItem()
        {
            if (currentMediaIndex >= videoList.Count-1)
            {
                currentMediaIndex = 0;
            }
            else
            {
                currentMediaIndex++;
            }
            
            SetNewMedia((String)videoList[currentMediaIndex]);
        }

        private void PreviousMediaItem()
        {
            if (currentMediaIndex <= 0)
            {
                currentMediaIndex = videoList.Count - 1;
            }
            else
            {
                currentMediaIndex--;
            }

            SetNewMedia((String)videoList[currentMediaIndex]);
        }

        private void SetNewMedia(String fileName)
        {
            bool gotDate = false;
            bool gotCaption = false;

            logger.InfoFormat("SetNewMedia - {0}", fileName);

            currentMediaPath = fileName;

            FullScreenMedia.Source = new System.Uri(fileName);

            // Read Metadata from the photo
            using (ExifReader reader = new ExifReader(fileName))
            {
                // Extract the tag data using the ExifTags enumeration

                try
                {

                    gotDate = reader.GetTagValue<DateTime>(ExifTags.DateTimeDigitized,
                                                out currentMediaDateTaken);
                }
                catch(InvalidCastException)
                {
                    gotDate = false;
                    currentMediaDateTaken = DateTime.MinValue;
                }

                try
                {
                    currentMediaTitle = GetTitle(fileName);
                }
                catch (InvalidCastException)
                {
                    gotCaption = false;
                    currentMediaTitle = "";
                }

                if (gotDate || gotCaption)
                {
                    PictureInfo.Text = 
                        (gotDate ? currentMediaDateTaken.ToLongDateString() : "") +
                        (gotCaption && currentMediaTitle.Length > 0 ? "- " + currentMediaTitle : "");
                }
                else
                {
                    // No EXIF properties - just use file name
                    PictureInfo.Text = fileName;
                }
            }
        }

        // Get the string Title if present in the file
        private string GetTitle(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            BitmapSource img = BitmapFrame.Create(fs);
            BitmapMetadata md = (BitmapMetadata)img.Metadata;
            return md.Title;
        }
    }
}
