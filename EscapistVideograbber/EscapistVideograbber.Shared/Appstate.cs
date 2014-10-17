using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Popups;
using GrabbingLib;

namespace EscapistVideograbber
{
    internal class Appstate
    {
        public static readonly Appstate state = new Appstate();

        private Appstate()
        {
        }

        public String EnteredURL { get; set; }
        public bool opendl { get; set; }
        public bool hq { get; set; }
        public bool autosave { get; set; }
        //public Windows.Storage.StorageFile file { get; set; }
    }

    internal class CommHelp
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
            var dialog = new MessageDialog(message, title);
            dialog.Commands.Add(new UICommand("OK"));
            await dialog.ShowAsync();
        }

        public static async Task<String> getAutoFilePath(String title)
        {
            String filename = title + ".mp4";
            StorageFolder folder =
                (await KnownFolders.VideosLibrary.GetFoldersAsync()).FirstOrDefault(
                    x => x.Name.Equals(Grabber.EscapistDir)) ??
                await KnownFolders.VideosLibrary.CreateFolderAsync(Grabber.EscapistDir);
            StorageFile file = (await folder.GetFilesAsync()).FirstOrDefault(x => x.Name.Equals(filename)) ??
                               await folder.CreateFileAsync(filename);
            return file.Path;
        }
    }

    internal class Downloadhelper : Downloader
    {
        private static DownloadOperation download;

        public Downloadhelper(Action<ulong, ulong> updatehandler, Action<string, bool> finishhandler)
            : base(updatehandler, finishhandler)
        {
        }

        public override void finishdl()
        {
            {
                if (download.Progress.Status != BackgroundTransferStatus.Completed ||
                    download.Progress.Status != BackgroundTransferStatus.Error ||
                    download.Progress.Status != BackgroundTransferStatus.Canceled)
                {
                    download.AttachAsync().Cancel();
                    finishhandler.Invoke(null, true);
                }
                download = null;
            }
        }

        public override async void startdownload(string sourceuri, string targeturi)
        {
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(targeturi);
                download = new BackgroundDownloader().CreateDownload(new Uri(sourceuri), file);
                await download.StartAsync().AsTask(new Progress<DownloadOperation>(dlop =>
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