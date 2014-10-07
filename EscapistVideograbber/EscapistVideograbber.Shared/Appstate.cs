using System;
using System.Collections.Generic;
using System.Text;

using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.Storage;
using Windows.Networking.BackgroundTransfer;
using System.Linq;

namespace EscapistVideograbber
{
    class Appstate
    {
        public static readonly Appstate state = new Appstate();

        public String EnteredURL { get; set; }
        public bool opendl { get; set; }
        public bool hq { get; set; }
        public bool autosave { get; set; }
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

        public static async Task<String> getAutoFilePath(String title)
        {
            String filename = title + ".mp4";
            StorageFolder folder = (await Windows.Storage.KnownFolders.VideosLibrary.GetFoldersAsync()).FirstOrDefault(x => x.Name.Equals(GrabbingLib.Grabber.EscapistDir));
            if (folder == null)
                folder = await Windows.Storage.KnownFolders.VideosLibrary.CreateFolderAsync(GrabbingLib.Grabber.EscapistDir);
            StorageFile file = (await folder.GetFilesAsync()).FirstOrDefault(x => x.Name.Equals(filename));
            if (file == null)
                file = await folder.CreateFileAsync(filename);
            return file.Path;
        }
    }

    class Downloadhelper : GrabbingLib.Downloader
    {
        private static DownloadOperation download;

        public Downloadhelper(Action<ulong, ulong> updatehandler, Action<string, bool> finishhandler)
            : base(updatehandler, finishhandler)
        {
        }

        public override void finishdl()
        {
            {
                if (download.Progress.Status != BackgroundTransferStatus.Completed || download.Progress.Status != BackgroundTransferStatus.Error || download.Progress.Status != BackgroundTransferStatus.Canceled)
                {
                    download.AttachAsync().Cancel();
                    this.finishhandler.Invoke(null, true);
                }
                download = null;
            }
        }

        public override async void startdownload(string sourceuri, string targeturi)
        {
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(targeturi);
                download = new BackgroundDownloader().CreateDownload(new Uri(sourceuri), file);
                await download.StartAsync().AsTask(new Progress<DownloadOperation>((DownloadOperation dlop) =>
                {
                    ulong received = dlop.Progress.BytesReceived;
                    ulong total = dlop.Progress.TotalBytesToReceive;
                    updatehandler.Invoke(received, total);
                    if (received == total)
                        finishhandler.Invoke(dlop.ResultFile.Path, false);
                }));
            }
        }
    }
}
