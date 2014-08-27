using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Threading;

namespace DesktopGrabber
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
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
            this.urlbox.Text = GrabbingLib.Grabber.ZPLatestURL;
        }

        private void pastebtn_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
                this.urlbox.Text = Clipboard.GetText();
        }

        private void purge()
        {
            this.urlbox.IsEnabled = true;
            this.latestzpbtn.IsEnabled = true;
            this.pastebtn.IsEnabled = true;
            this.openchkbox.IsEnabled = true;
            this.startbtn.IsEnabled = true;
            this.cancelbtn.IsEnabled = false;
            this.proglabel.Content = "";
            this.progbar.Value = 0;
            this.progbar.IsIndeterminate = false;
            this.taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            this.taskbar.ProgressValue = 0.0;
            this.tokensource.Cancel();
            GrabbingLib.Grabber.finishDL();
        }

        private async void startcancelbtn_Click(object sender, RoutedEventArgs e)
        {
            this.urlbox.IsEnabled = false;
            this.latestzpbtn.IsEnabled = false;
            this.pastebtn.IsEnabled = false;
            this.openchkbox.IsEnabled = false;
            this.startbtn.IsEnabled = false;
            this.cancelbtn.IsEnabled = true;
            this.progbar.IsIndeterminate = true;
            this.taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
            this.proglabel.Content = "Loading website";
            var task = GrabbingLib.Grabber.evaluateURL(this.urlbox.Text, this.showerror, () =>
            {
                this.proglabel.Content = "Loading video data";
            }, () =>
            {
                this.proglabel.Content = "Starting video download";
            }, this.FileChooser, new Downloadhelper((ulong received, ulong total) =>
            {
                double progress = ((double) received / total) * 100;
                progbar.Value = progress;
                progbar.IsIndeterminate = false;
                taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
                taskbar.ProgressValue = progress / 100.0;
                proglabel.Content = "Download running - " + (int) progress + " % ( "
                    + GrabbingLib.Grabber.ByteSize(received) + " / "
                    + GrabbingLib.Grabber.ByteSize(total) + " )";
            }, (String filepath) =>
            {
                if (openchkbox.IsChecked != null && openchkbox.IsChecked.Value)
                    System.Diagnostics.Process.Start(filepath);
                else
                    MessageBox.Show("The download is complete", "Task complete");
                purge();
            }), this.showmessage);
            
        }

        private async Task showerror(Exception e)
        {
            MessageBox.Show(e.ToString(), "Error occured");
        }

        private async Task showmessage(String message)
        {
            MessageBox.Show(message);
        }

        private async Task<String> FileChooser(String title)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.DefaultExt = ".mp4";
            dialog.FileName = title;
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            dialog.Filter = "MP4 Video Files |*.mp4";
            if (dialog.ShowDialog().Value)
                return dialog.FileName;
            else
                return null;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.purge();
        }
    }
}
