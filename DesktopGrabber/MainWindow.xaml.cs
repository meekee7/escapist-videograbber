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
            hqchkbox.IsEnabled = true;
            autosavechkbox.IsEnabled = true;
            startbtn.IsEnabled = true;
            cancelbtn.IsEnabled = false;
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

        private async Task<String> FileChooser(String title)
        {
            if (autosavechkbox.IsChecked.HasValue && autosavechkbox.IsChecked.Value)
            {
                string videopath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) + '\\' +
                                   Grabber.EscapistDir;
                if (!Directory.Exists(videopath))
                    Directory.CreateDirectory(videopath);
                return videopath + '\\' + title + ".mp4";
            }
            var dialog = new SaveFileDialog
            {
                DefaultExt = ".mp4",
                FileName = title,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Filter = "MP4 Video Files |*.mp4"
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
            urlbox.IsEnabled = false;
            latestzpbtn.IsEnabled = false;
            pastebtn.IsEnabled = false;
            openchkbox.IsEnabled = false;
            hqchkbox.IsEnabled = false;
            autosavechkbox.IsEnabled = false;
            startbtn.IsEnabled = false;
            cancelbtn.IsEnabled = true;
            progbar.IsIndeterminate = true;
            taskbar.ProgressState = TaskbarItemProgressState.Indeterminate;
            proglabel.Content = "Loading website";
            await
                Grabber.evaluateURL(urlbox.Text, hqchkbox.IsChecked != null && hqchkbox.IsChecked.Value, showerror,
                    () => { proglabel.Content = "Loading video data"; },
                    () => { proglabel.Content = "Starting video download"; }, FileChooser,
                    new Downloadhelper((received, total) =>
                    {
                        double progress = ((double) received / total) * 100;
                        progbar.Value = progress;
                        progbar.IsIndeterminate = false;
                        taskbar.ProgressState = TaskbarItemProgressState.Normal;
                        taskbar.ProgressValue = progress / 100.0;
                        proglabel.Content = "Download running - " + (int) progress + " % ( "
                                            + Grabber.ByteSize(received) + " / "
                                            + Grabber.ByteSize(total) + " )";
                    }, delegate(string filepath, bool wascancelled)
                    {
                        if (!wascancelled)
                            if (openchkbox.IsChecked != null && openchkbox.IsChecked.Value)
                                Process.Start(filepath);
                            else
                                MessageBox.Show("The download is complete. The file was saved to " + filepath,
                                    "Task complete");
                        purge();
                    }), showmessage, purge, tokensource.Token);
        }

        private void clearbtn_Click(object sender, RoutedEventArgs e)
        {
            urlbox.Text = String.Empty;
        }

        private async void probebtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(await Grabber.getLatestZPTitle(), "Current ZP Episode");
        }
    }
}