using LibVLCSharp.Shared;
using video_trimmer.Processing;
using Xabe.FFmpeg;
using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Users;

namespace video_trimmer
{
    public partial class VideoTrimmerForm : Form
    {


        private string customerId = string.Empty;
        private const string AccessToken = "sl.BkDY5dfrOjNLtEWojHupCiCQzJvH61cZa2way1ztbcdEVgnsxOQ7-0aoDeVATe1pbT4ES5G81U1sK6nlLk6BW1IqRiaKMgXmuqjOa6JCOTt4eAtJbbCNcsmRy3KvZmt1Gij_sDHwmZ-Q";
        private const string DestinationPath = "/"; // Change to your desired destination path

        private DropboxClient dropboxClient;

        private delegate void SafeCallDelegate(string text);
        private string SelectedFile;
        private string SelectedDirectory;
        // VLC Player 1
        public LibVLC LibVLCOne;
        public MediaPlayer mediaPlayerOne;
        public Media mediaOne;
        // VLC Player 2
        public MediaPlayer mediaPlayerTwo;
        public Media mediaTwo;

        private readonly IProcessorManager ProcessorManager;
        private readonly IVideoProcessor VideoProcessor = new VideoProcessor();

        public VideoTrimmerForm(string customerId)
        {
            InitializeComponent();
            Core.Initialize();
            //this.KeyPreview = true;
            // VLC Player 1
            LibVLCOne = new LibVLC();
            mediaPlayerOne = new MediaPlayer(LibVLCOne);
            videoView.MediaPlayer = mediaPlayerOne;

            // VLC Player 2
            mediaPlayerTwo = new MediaPlayer(LibVLCOne);
            videoView2.MediaPlayer = mediaPlayerTwo;

            ProcessorManager = new ProcessorManager();
            ProcessorManager.UpdateHandler = UpdateProgressStatus;

            this.customerId = customerId;
        }
        private async Task LoadFilesAsync()
        {
            this.DirectoryLabel.Text = $"Select Directory\n{SelectedDirectory}";
            string[] files = Directory.GetFiles(SelectedDirectory, "*.mp4");
            string[] filesOnly = files.Select(file => Path.GetFileName(file)).ToArray();

            foreach ( string file in filesOnly)
            {
                IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(file);
                var videoDuration = mediaInfo.VideoStreams.First().Duration;

            }
            FileListBox.Items.AddRange(filesOnly);
        }

        private async void QueueJobButton_Click(object sender, EventArgs e)
        {
            // Fetch the start time and end time
            long trimStartSeconds = mediaPlayerOne.Time;
            long trimEndSeconds = mediaPlayerTwo.Time;
            TimeSpan startTime = TimeSpan.FromMilliseconds(trimStartSeconds);
            TimeSpan endTime = TimeSpan.FromMilliseconds(trimEndSeconds);

            // input file
            string inputFile = Path.Combine(SelectedDirectory, SelectedFile);
            string outputFile = Path.Combine(SelectedDirectory, $"temp-{SelectedFile}");

            // close out the media players
            mediaPlayerOne.Stop();
            mediaPlayerTwo.Stop();
            // start a new thread to create the new video file
            IVideoProcessor processor = new VideoProcessor();
            await processor.ConversionSetup(inputFile, outputFile, startTime, endTime, OverwriteCheckBox.Checked);
            ProcessorManager.AddProcessor(processor);

        }

        private async Task SelectDirectoryButton_ClickAsync(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                FileListBox.Items.Clear();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    SelectedDirectory = fbd.SelectedPath;
                    Properties.Settings.Default.DefaultDirectory = SelectedDirectory;
                    Properties.Settings.Default.Save();
                    await LoadFilesAsync();
                    //System.Windows.Forms.MessageBox.Show("Files found: " + files.Length.ToString(), "Message");
                }
            }
        }

        private async void FileListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedFile = (string)FileListBox.SelectedItem;
            string videoURL = Path.Combine(SelectedDirectory, SelectedFile);
            mediaPlayerOne.Media = new Media(LibVLCOne, videoURL);
            mediaPlayerTwo.Media = new Media(LibVLCOne, videoURL);
           
            await mediaPlayerOne.Media.Parse();
            await mediaPlayerTwo.Media.Parse();
            StatsLabel.Text = $"STATS: {SelectedFile} \nDuration:{TimeSpan.FromMilliseconds(mediaPlayerOne.Media.Duration)}";
            mediaPlayerOne.SeekTo(TimeSpan.FromSeconds(60));
            mediaPlayerOne.Volume = 0;
            mediaPlayerOne.PositionChanged += MediaPlayer_PositionChanged;
            mediaPlayerOne.Play();
            // update media Player two
            TimeSpan duration = TimeSpan.FromMilliseconds(mediaPlayerOne.Media.Duration);
            mediaPlayerTwo.SeekTo(duration.Subtract(TimeSpan.FromSeconds(60)));
            mediaPlayerTwo.Volume = 0;
            mediaPlayerTwo.PositionChanged += MediaPlayerTwo_PositionChanged;
            mediaPlayerTwo.Play();
        }

        private async Task UploadVideoFilesToDropBoxAsync()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files (*.mp4, *.avi, *.mov)|*.mp4;*.avi;*.mov";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(filePath);
                string targetPath = Path.Combine(DestinationPath, fileName);

                try
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        var uploadResult = await dropboxClient.Files.UploadAsync(targetPath, WriteMode.Overwrite.Instance, body: stream);
                        MessageBox.Show("File uploaded successfully!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error uploading file: {ex.Message}");
                }
            }
        }

        private void MediaPlayerTwo_PositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
        {
            mediaPlayerTwo.Pause();
        }

        private void MediaPlayer_PositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
        {
            mediaPlayerOne.Pause();
        }

        private void MediaPlayerOne_Playing(object? sender, EventArgs e)
        {
            //mediaPlayerOne.NextFrame();
            //mediaPlayerOne.Pause();
            //mediaPlayerOne.NextFrame();

        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            //Todo update the current position within the first 5 minutes of the 
            TimeSpan target = TimeSpan.FromSeconds(trackBar1.Value);
            mediaPlayerOne.SeekTo(target);
            StartTrimLabel.Text = $"Start Trim: {target.ToString(@"hh\:mm\:ss")}";
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {

        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan target = TimeSpan.FromMilliseconds(mediaPlayerTwo.Media.Duration + (trackBar2.Value * 1000));
            // TrackBar2 value is between 0(the end), and -600(10 minutes from the end).
            mediaPlayerTwo.SeekTo(target);
            EndTrimLabel.Text = $"End Trim: {target.ToString(@"hh\:mm\:ss")}";    
        }

        private void UpdateProgressStatus((int jobs, double progress) status)
        {
            WriteProgressStatus($"Active Jobs:{status.jobs} Pending Jobs:{this.ProcessorManager.ProcessorQueue.Count} Average Progress:{status.progress}");
        }

        private void WriteProgressStatus(string text)
        {
            if (StatusLabel.InvokeRequired)
            {
                var d = new SafeCallDelegate(WriteProgressStatus);
                StatusLabel.Invoke(d, new object[] { text });
            }
            else
            {
                StatusLabel.Text = text;
            }
        }
    }
}