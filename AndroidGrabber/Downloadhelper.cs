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
    internal abstract class UserAction
    {
    }

    internal class GrabVideo : UserAction
    {
        public GrabVideo(ParsingRequest request, bool opendl, bool autosave)
        {
            this.request = request;
            this.opendl = opendl;
            this.autosave = autosave;
        }

        public ParsingRequest request { get; private set; }
        public bool opendl { get; private set; }
        public bool autosave { get; private set; }
    }

    internal class GetLatestZP : UserAction
    {
    }

    internal class WaitForNewZP : GrabVideo
    {
        public WaitForNewZP(ParsingRequest request, bool opendl, bool autosave)
            : base(request, opendl, autosave)
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
            container = ParsingRequest.CONTAINER.C_MP4;
            resolution = ParsingRequest.RESOLUTION.R_480P;
            autosave = true;
        }

        public UserAction currentaction { get; set; }

        public String EnteredURL { get; set; }
        public bool opendl { get; set; }
        public ParsingRequest.CONTAINER container;
        public ParsingRequest.RESOLUTION resolution;
        public bool autosave { get; set; }
    }

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