using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;

namespace DesktopGrabber
{
    class Utils
    {
    }
    public class Downloadhelper : GrabbingLib.Downloader
    {
        private WebClient download = null;
        public Downloadhelper(Action<ulong, ulong> updatehandler, Action<string> finishhandler) : base(updatehandler,finishhandler)
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
            download.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
            {
                updatehandler.Invoke((ulong) e.BytesReceived, (ulong) e.TotalBytesToReceive);
            };
            download.DownloadFileCompleted += delegate(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
            {
                finishhandler.Invoke(targeturi);
            };
            download.DownloadFileAsync(new Uri(sourceuri), targeturi);
        }
    }
}
