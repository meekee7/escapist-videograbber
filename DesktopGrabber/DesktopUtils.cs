using System;
using System.Net;
using GrabbingLib;

namespace DesktopGrabber
{
    public class Downloadhelper : Downloader
    {
        private WebClient download;

        public Downloadhelper(Action<ulong, ulong> updatehandler, Action<string, bool> finishhandler)
            : base(updatehandler, finishhandler)
        {
        }

        public override void finishdl()
        {
            if (download != null)
            {
                download.CancelAsync();
                download = null;
            }
        }

        public override void startdownload(string sourceuri, string targeturi)
        {
            download = new WebClient();
            download.DownloadProgressChanged +=
                (sender, e) => updatehandler.Invoke((ulong) e.BytesReceived, (ulong) e.TotalBytesToReceive);
            download.DownloadFileCompleted += (sender, e) => finishhandler.Invoke(targeturi, e.Cancelled);
            download.DownloadFileAsync(new Uri(sourceuri), targeturi);
        }
    }
}