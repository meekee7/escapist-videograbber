using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using GrabbingLib;
using Microsoft.Win32;

namespace DesktopGrabber
{
    /// <summary>
    ///     Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource tokensource = new CancellationTokenSource();

        public MainWindow()
        {
            InitializeComponent();
        }

        private Action Jsonaction
        {
            get { return () => proglabel.Content = "Starting video download"; }
        }

        private Action Htmlaction
        {
            get { return () => proglabel.Content = "Loading video data"; }
        }

        private Action<string, bool> Finishhandler
        {
            get
            {
                return (filepath, wascancelled) =>
                {
                    if (!wascancelled)
                    {
                        if (openchkbox.IsChecked != null && openchkbox.IsChecked.Value)
                            Process.Start(filepath);
                        else
                            MessageBox.Show("The download is complete. The file was saved to " + filepath,
                                "Task complete");
                        urlbox.Text = "";
                    }
                    purge();
                };
            }
        }

        private Action<ulong, ulong> Updatehandler
        {
            get
            {
                return (received, total) =>
                {
                    double progress = ((double) received / total) * 100;
                    progbar.Value = progress;
                    progbar.IsIndeterminate = false;
                    taskbar.ProgressState = TaskbarItemProgressState.Normal;
                    taskbar.ProgressValue = progress / 100.0;
                    proglabel.Content = "Download running - " + (int) progress + " % ( "
                                        + Grabber.ByteSize(received) + " / "
                                        + Grabber.ByteSize(total) + " )";
                };
            }
        }

        private void latestzpbtn_Click(object sender, RoutedEventArgs e)
        {
            urlbox.Text = Grabber.ZPLatestURL;
            startdl();
        }

        private void pastebtn_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                urlbox.Text = Clipboard.GetText();
                startdl();
            }
        }

        private void purge()
        {
            urlbox.IsEnabled = true;
            latestzpbtn.IsEnabled = true;
            pastebtn.IsEnabled = true;
            openchkbox.IsEnabled = true;
            // hqchkbox.IsEnabled = true;
            autosavechkbox.IsEnabled = true;
            startbtn.IsEnabled = true;
            cancelbtn.IsEnabled = false;
            awaitbtn.IsEnabled = true;
            proglabel.Content = "";
            progbar.Value = 0;
            progbar.IsIndeterminate = false;
            taskbar.ProgressState = TaskbarItemProgressState.None;
            taskbar.ProgressValue = 0.0;
            tokensource.Cancel();
            Grabber.finishDL();
            tokensource = new CancellationTokenSource();
        }

        private void startcancelbtn_Click(object sender, RoutedEventArgs e)
        {
            startdl();
        }

        private async Task showerror(Exception e)
        {
            MessageBox.Show(e.ToString(), "Error occured");
            purge();
        }

        private async Task showmessage(String message)
        {
            MessageBox.Show(message);
        }

        private async Task<String> FileChooser(String title, ParsingRequest.CONTAINER container)
        {
            String extension = container == ParsingRequest.CONTAINER.C_MP4 ? ".mp4" : ".webm";
            if (autosavechkbox.IsChecked.HasValue && autosavechkbox.IsChecked.Value)
            {
                string videopath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) + '\\' +
                                   Grabber.EscapistDir;
                if (!Directory.Exists(videopath))
                    Directory.CreateDirectory(videopath);
                return videopath + '\\' + title + extension;
            }
            var dialog = new SaveFileDialog
            {
                DefaultExt = extension,
                FileName = title,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Filter = container == ParsingRequest.CONTAINER.C_MP4 ? "MP4 Video Files |*.mp4" : "WebM Video Files |*.webm"
            };
            bool? showDialog = dialog.ShowDialog();
            if (showDialog != null && showDialog.Value)
                return dialog.FileName;
            return null;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            proglabel.Content = "Cancelling";
            purge();
        }

        private void cancelbtn_Click(object sender, RoutedEventArgs e)
        {
            purge();
        }

        private void urlbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                startdl();
        }

        private async void startdl()
        {
            lockup();
            var res = RB360p.IsChecked != null && RB360p.IsChecked.Value
                ? ParsingRequest.RESOLUTION.R_360P
                : ParsingRequest.RESOLUTION.R_480P;
            var type = RBMP4.IsChecked != null && RBMP4.IsChecked.Value
                ? ParsingRequest.CONTAINER.C_MP4
                : ParsingRequest.CONTAINER.C_WEBM;
            await
                Grabber.evaluateURL(new ParsingRequest(urlbox.Text, res, type), showerror,
                    Htmlaction, Jsonaction, FileChooser, new Downloadhelper(Updatehandler, Finishhandler), showmessage,
                    purge, tokensource.Token);
        }

        private void clearbtn_Click(object sender, RoutedEventArgs e)
        {
            urlbox.Text = String.Empty;
        }

        private async void probebtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(await Grabber.getLatestZPTitle(), "Current ZP Episode");
        }

        private async void awaitbtn_Click(object sender, RoutedEventArgs e)
        {
            lockup();
            await
                Grabber.waitForNewZPEpisode(tokensource.Token,
                    async oldtitle => MessageBox.Show("Please confirm that this is the old episode: " + oldtitle,
                        "Confirm old episode", MessageBoxButton.YesNo) == MessageBoxResult.Yes, async () =>
                        {
                            await showmessage("Timeout: maximum number of attempts reached");
                            purge();
                        },
                    attempt => proglabel.Content = "Attempt: " + attempt, () =>
                    {
                        //No specific action
                    }, Htmlaction, Jsonaction, FileChooser, new Downloadhelper(Updatehandler, Finishhandler),
                    showmessage, purge, showerror);
        }

        private void lockup()
        {
            urlbox.IsEnabled = false;
            latestzpbtn.IsEnabled = false;
            pastebtn.IsEnabled = false;
            openchkbox.IsEnabled = false;
            //hqchkbox.IsEnabled = false;
            autosavechkbox.IsEnabled = false;
            startbtn.IsEnabled = false;
            awaitbtn.IsEnabled = false;
            cancelbtn.IsEnabled = true;
            progbar.IsIndeterminate = true;
            taskbar.ProgressState = TaskbarItemProgressState.Indeterminate;
            proglabel.Content = "Loading website";
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}