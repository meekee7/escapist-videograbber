using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using GrabbingLib;

namespace AndroidGrabber
{
    class Downloadhelper : Downloader
    {
        private WebClient download;
        public Downloadhelper(Action<ulong, ulong> updatehandler, Action<string, bool> finishhandler) : base(updatehandler, finishhandler)
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
                (sender, e) => updatehandler.Invoke((ulong)e.BytesReceived, (ulong)e.TotalBytesToReceive);
            download.DownloadFileCompleted += (sender, e) => finishhandler.Invoke(targeturi, e.Cancelled);
            download.DownloadFileAsync(new Uri(sourceuri), targeturi);
        }
    }
}