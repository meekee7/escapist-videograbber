using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
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

    internal class WaitForNewZP : GrabVideo
    {
        public WaitForNewZP(bool opendl, bool autosave)
            : base(Grabber.ZPLatestURL, opendl, false, autosave)
        {
        }
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
            await showmessage(message, ResourceLoader.GetForCurrentView().GetString("msg/DefaultTitle"));
        }

        public static async Task showmessage(String message, String title)
        {
            try
            {
                var dialog = new MessageDialog(message, title);
                dialog.Commands.Add(new UICommand(ResourceLoader.GetForCurrentView().GetString("msg/OK")));
                await dialog.ShowAsync();
            }
            catch (Exception e) //turns out that showAsync is not thread-safe and multiple message dialoges
            { //At the same time lead to an obscure exception
                e.ToString(); //That is just here so that we have a breakpoint for the debugger
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
            if (download != null)
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
            StorageFile file = await StorageFile.GetFileFromPathAsync(targeturi);
            download = new BackgroundDownloader().CreateDownload(new Uri(sourceuri), file);
            //We do not await here, this would lead to exepctions when the download is cancelled
            download.StartAsync().AsTask(new Progress<DownloadOperation>(dlop =>
            {
                ulong received = dlop.Progress.BytesReceived;
                ulong total = dlop.Progress.TotalBytesToReceive;
                updatehandler.Invoke(received, total);
                if (received == total)
                    //BackgroundTransferStatus.Completed does not occur during progress or as an event, so we look at the numbers
                    finishhandler.Invoke(dlop.ResultFile.Path, false);
            }));
        }
    }
}