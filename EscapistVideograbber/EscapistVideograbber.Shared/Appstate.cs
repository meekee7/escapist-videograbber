using System;
using System.Collections.Generic;
using System.Text;

using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.Storage;
using Windows.Networking.BackgroundTransfer;

namespace EscapistVideograbber
{
    class Appstate
    {
        public static readonly Appstate state = new Appstate();

        public String EnteredURL { get; set; }
        public bool opendl { get; set; }
        //public Windows.Storage.StorageFile file { get; set; }

        private Appstate()
        {
        }
    }

    class CommHelp
    {
        private CommHelp()
        {
        }

        public static async Task showmessage(String message)
        {
            await showmessage(message, "Hinweis");
        }

        public static async Task showmessage(String message, String title)
        {
            MessageDialog dialog = new MessageDialog(message, title);
            dialog.Commands.Add(new UICommand("OK"));
            await dialog.ShowAsync();
        }
    }

    class Downloadhelper : GrabbingLib.Downloader
    {
        private static DownloadOperation download;
        public override void finishdl()
        {
            {
                if (download.Progress.Status != BackgroundTransferStatus.Completed || download.Progress.Status != BackgroundTransferStatus.Error || download.Progress.Status != BackgroundTransferStatus.Canceled)
                    download.AttachAsync().Cancel();
                download = null;
            }
        }

        public override async void startdownload(string sourceuri, string targeturi)
        {
            {
                //dlop.resultfile.path
                StorageFile file = await StorageFile.GetFileFromPathAsync(targeturi);
                download = new BackgroundDownloader().CreateDownload(new Uri(sourceuri), file);
                await download.StartAsync().AsTask(new Progress<DownloadOperation>((DownloadOperation dlop) =>
                {
                    updatehandler.Invoke(dlop.Progress.BytesReceived, dlop.Progress.TotalBytesToReceive);
                }));
            }
        }
    }
}
