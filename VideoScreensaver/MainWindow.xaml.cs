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
using System.Timers;
using System.Threading;
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

        public bool Debug { get; set; }

        private bool preview;

        private VideoList videoList;

        private Point? lastMousePosition = null;  // Workaround for "MouseMove always fires when maximized" bug.

        private int currentMediaIndex = 0;

        private bool showingInfoDialog = false;     // TRUE if showing properties for a picture

        System.Windows.Threading.DispatcherTimer dispatcherTimer = null;

        private string cacheFileName;

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
                // Adjust sizes of text down
                PictureInfo.FontSize = 9;
                GeneralData.FontSize = 9;
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

                case Key.H:
                case Key.Help:
                case Key.OemQuestion:
                case Key.F1:
                    ShowHelpAbout();
                    break;

                case Key.D:     // Delete
                    if (currentMediaPath.Length > 0) {
                        logger.InfoFormat("ScrKeyDown - Delete {0}", currentMediaPath);
                        try
                        {
                            string previousMediaPath = currentMediaPath;

                            // Advance to free it from being held open by the MediaElement displaying it
                            NextMediaItem();

                            MediaOperations.RecycleFile(previousMediaPath);

                            DrawRedX();
                        }
                        catch (OperationCanceledException exp)
                        {
                            // user cancelled
                        }
                        catch (InvalidOperationException exp)
                        {
                            ShowError(exp.Message);
                        }
                    }
                    break;

                case Key.C:     // Copy
                    if (currentMediaPath.Length > 0) {
                        logger.InfoFormat("ScrKeyDown - Copy to clipboard {0}", currentMediaPath);
                        MediaOperations.CopyToClipboard(currentMediaPath);
                        // UNDONE: Visual Feedback - flash picture
                    }
                    break;

                default:
                    logger.InfoFormat("ScrKeyDown - Ending due to unexpected Windows key message {0}", e.ToString());
                    EndFullScreensaver();
                    break;
            }
        }

        private void DrawRedX()
        {
            Line line = new Line();
            Thickness thickness = new Thickness(10);
            line.Margin = thickness;
            line.Visibility = System.Windows.Visibility.Visible;
            line.StrokeThickness = 4;
            line.Stroke = System.Windows.Media.Brushes.Red;
            line.X1 = 0;
            line.X2 = 0;
            line.Y1 = this.ActualHeight;
            line.Y2 = this.ActualWidth;
            MainGrid.Children.Add(line);
        }

        // Show the Help/About window
        private void ShowHelpAbout()
        {
            showingInfoDialog = true;           // stop the show

            Window helpAboutWindow = new HelpAbout();

            helpAboutWindow.ShowDialog();

            logger.Info("Help/About dismissed");

            showingInfoDialog = false;

            // don't immediately see a mouse move
            lastMousePosition = null;

            // Advance since we've stopped auto advancing while the properties dialog was up
            NextMediaItem();
        }

        private void ShowInfo()
        {
            showingInfoDialog = true;

            PropertiesControl infoControl = new PropertiesControl();

            infoControl.PhotoTitle = currentMediaTitle;
            infoControl.PhotoTimeTaken = currentMediaDateTaken;
            infoControl.PhotoFilePath = currentMediaPath;

            logger.InfoFormat("ShowInfo for {0} - {1}", currentMediaIndex, currentMediaPath);

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
            logger.Info("ShowInfo dismissed");
            showingInfoDialog = false;
            // don't immediately see a mouse move
            lastMousePosition = null;
            // Advance since we've stopped auto advancing while the properties dialog was up
            NextMediaItem();
        }

        private void ScrMouseWheel(object sender, MouseWheelEventArgs e) {
            volume += e.Delta / 1000.0;
        }

        private void ScrMouseMove(object sender, MouseEventArgs e) {
            // Workaround for bug in WPF.
            Point mousePosition = e.GetPosition(this);
            if (lastMousePosition != null && mousePosition != lastMousePosition) {
                logger.Info("Ending due to mouse movement");
                // To simply debugging, don't actual exit if in debug mode
                if (!Debug)
                {
                    EndFullScreensaver();
                }
            }
            lastMousePosition = mousePosition;
        }

        private void ScrMouseDown(object sender, MouseButtonEventArgs e) {
            logger.Info("Ending due to mouse click");
            // To simply debugging, don't actual exit if in debug mode
            if (!Debug)
            {
                EndFullScreensaver();
            }
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

        // Callback from MediaElement - not currently used
        private void MediaEnded(object sender, RoutedEventArgs e)
        {
            logger.Info("MediaEnded");
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
            try
            {
                String videoPath = PreferenceManager.ReadVideoSettings();
                string machineName = System.Environment.MachineName;

                logger.Info("OnLoaded");

                if (videoPath.Length == 0)
                {
                    // Default to Pictures
                    videoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                }

                logger.InfoFormat("Pictures Path - {0}", videoPath);

                if (!Directory.Exists(videoPath))
                {
                    ShowError("This screensaver needs to be configured before anthing is displayed.");
                }
                else
                {
                    // Is there a searilized list?
                    DirectoryInfo appDataFolder = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\VideoScreenSaver");
                    if (!appDataFolder.Exists)
                    {
                        appDataFolder.Create();
                    }

                    cacheFileName = System.IO.Path.Combine(appDataFolder.ToString(), "ScreenSaver.data.bin");

                    if (File.Exists(cacheFileName))
                    {
                        logger.InfoFormat("Cached pictures list {0} exists", cacheFileName);
                        try
	                    {
                            using (Stream stream = File.Open(cacheFileName, FileMode.Open))
			                {
			                    BinaryFormatter bin = new BinaryFormatter();

			                    videoList = (VideoList)bin.Deserialize(stream);

                                if (videoList.MachineName == null ||
                                    videoList.MachineName != machineName)
                                {
                                    // Not from this machine - throw it away
                                    videoList.Clear();   // throw away any cached info, it will be rebuilt
                                }
		                    }
                        }
		                catch (IOException ex)
		                {
                            logger.Error("Deserializing video list", ex);
                            videoList.Clear();   // throw away any cached info, it will be rebuilt
		                }
                        logger.InfoFormat("Read {0} picture paths from cached file", videoList.Count);
                    }
                }

                if (videoList.Count == 0)
                    {
                        logger.InfoFormat("No cached pictures list {0} exists - enumerating files", cacheFileName);

                        GetAllVideos(videoPath);

                        videoList.MachineName = machineName;

                        // Serialize it to disk
                        try
                        {
                            logger.Info("Writing enumerated paths to disk cache");
                            using (Stream stream = File.Open(cacheFileName, FileMode.Create))
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

                // http://robertgreiner.com/2010/06/using-stopwatches-and-timers-in-net/
                // We used to use a callback from showing the media to trigger the next one
                // however, if anything fails in showing a picture, it doesn't make the call.
                // So now we just have a timer go off
                // UNDONE: Make the interval a configuration parameter

                dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(TimedAdvance);
                dispatcherTimer.Interval = new TimeSpan(0,0,8);     // 8 seconds
                dispatcherTimer.Start();

                // UNDONE - what if this fails - consolidate error handling
                SetNewMedia((String)videoList[currentMediaIndex]);
            }
            catch (Exception exp)
            {
                logger.Error("OnLoaded", exp);
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

        // Callback fromm timer to advance to media.
        private void TimedAdvance(object sender, EventArgs e)
        {
            logger.Info("TimedAdvanceToNextMediaItem");
            if (!showingInfoDialog) {
                NextMediaItem();
            }
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
            
            while (!SetNewMedia((String)videoList[currentMediaIndex])) {
                // Skip bogus files
                if (currentMediaIndex >= videoList.Count-1)
                {
                    // Gone through all of them - delete the cache
                    break;
                }
                else
                {
                    currentMediaIndex++;
                }
            }
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

            // UNDONE - handle failure
            SetNewMedia((String)videoList[currentMediaIndex]);
        }

        private bool SetNewMedia(String fileName)
        {
            bool gotDate = false;
            bool gotCaption = false;

            logger.InfoFormat("SetNewMedia - {0}", fileName);

            currentMediaPath = fileName;

            System.Uri mediaUri = new System.Uri(fileName);

            if (mediaUri.IsFile)
            {
                if (!File.Exists(fileName))
                {
                    logger.WarnFormat("SetNewMedia - {0} does not exist", fileName);
                    return false;
                }
            }

            FullScreenMedia.Source = mediaUri;

            // Read Metadata from the photo
            logger.InfoFormat("SetNewMedia - Extract meta data from {0}", fileName);

            try
            {
                    using (ExifReader reader = new ExifReader(fileName))
                    {
                        // Extract the tag data using the ExifTags enumeration

                        try
                        {

                            gotDate = reader.GetTagValue<DateTime>(ExifTags.DateTimeDigitized,
                                                        out currentMediaDateTaken);
                            logger.InfoFormat("SetNewMedia - Date {0}", currentMediaDateTaken.ToString());
                        }
                        catch (InvalidCastException)
                        {
                            gotDate = false;
                            currentMediaDateTaken = DateTime.MinValue;
                            logger.Info("SetNewMedia - No Date Taken");
                        }

                        try
                        {
                            currentMediaTitle = GetTitle(fileName);
                            logger.InfoFormat("SetNewMedia - Title {0}", currentMediaTitle);
                        }
                        catch (InvalidCastException)
                        {
                            gotCaption = false;
                            currentMediaTitle = "";
                            logger.Info("SetNewMedia - No Title");
                        }
                    }
                }
                catch(ExifLibException exp)
                {
                    logger.ErrorFormat("SetNewMedia Reading EXIF info from {0}", fileName, exp.Message);
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
          
            logger.Info("SetNewMedia - Exit");
            return true;
        }

        // Get the string Title if present in the file
        private string GetTitle(string fileName)
        {
            try
            {
                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs != null)
                {
                    BitmapSource img = BitmapFrame.Create(fs);

                    if (img != null)
                    {
                        BitmapMetadata md = (BitmapMetadata)img.Metadata;

                        if (md != null)
                        {
                            return md.Title.TrimStart();
                        }
                    }
                }            
            }
            catch(Exception exp)
            {
                logger.Error("GetTitle", exp);
            }
            return string.Empty;
        }
    }
}
