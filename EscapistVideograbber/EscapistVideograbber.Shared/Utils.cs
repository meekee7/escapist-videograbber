using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Popups;
using GrabbingLib;

namespace EscapistVideograbber
{
    internal abstract class UserAction
    {
    }

    internal class GrabVideo : UserAction
    {
        public GrabVideo(string enteredURL, bool opendl, bool hq, bool autosave)
        {
            this.enteredURL = enteredURL;
            this.opendl = opendl;
            this.hq = hq;
            this.autosave = autosave;
        }

        public String enteredURL { get; private set; }
        public bool opendl { get; private set; }
        public bool hq { get; private set; }
        public bool autosave { get; private set; }
    }

    internal class GetLatestZP : UserAction
    {
    }

    internal class Appstate
    {
        public static readonly Appstate state = new Appstate();

        private Appstate()
        {
            EnteredURL = String.Empty;
            opendl = false;
            hq = true;
            autosave = true;
        }

        public UserAction currentaction { get; set; }

        public String EnteredURL { get; set; }
        public bool opendl { get; set; }
        public bool hq { get; set; }
        public bool autosave { get; set; }
    }

    internal class CommHelp
    {
        private CommHelp()
        {
        }

        public static async Task showmessage(String message)
        {
            await showmessage(message, "Hinweis"); //TODO get title from resources
        }

        public static async Task showmessage(String message, String title)
        {
            try
            {
                var dialog = new MessageDialog(message, title);
                dialog.Commands.Add(new UICommand("OK")); //TODO get string from resources
                await dialog.ShowAsync();
            }
            catch (Exception e)
            {
                e.ToString();
            }
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

        internal class DisplayMessage
        {
            internal readonly String message;
            internal readonly String title;

            public DisplayMessage(string title, string message)
            {
                this.title = title;
                this.message = message;
            }
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
            if (download != null) {
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
                    if (received == total) //BackgroundTransferStatus.Completed does not occur on its own, so we look at the numbers
                        finishhandler.Invoke(dlop.ResultFile.Path, false);
                }));
            }
        }
    }
}