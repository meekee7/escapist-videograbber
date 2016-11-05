using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Webkit;
using GrabbingLib;

namespace AndroidGrabber
{
    [Activity(Label = "AndroidGrabber", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private static readonly int permissioncode = 5; //Arbitrary
        private readonly CancellationTokenSource tokensource = new CancellationTokenSource();

        private async Task startdl(string url)
        {
            var res = FindViewById<RadioButton>(Resource.Id.res360pchc).Checked
                ? ParsingRequest.RESOLUTION.R_360P
                : ParsingRequest.RESOLUTION.R_480P;
            var type = FindViewById<RadioButton>(Resource.Id.typeMP4chc).Checked
                ? ParsingRequest.CONTAINER.C_MP4
                : ParsingRequest.CONTAINER.C_WEBM;
            await
                Grabber.evaluateURL(new ParsingRequest(url, res, type), showerror,
                    () => FindViewById<TextView>(Resource.Id.statustxt).Text = "Loading website",
                    () => FindViewById<TextView>(Resource.Id.statustxt).Text = "Loading video data", FileChooser, new Downloadhelper(
                        Updatehandler, Finishhandler),
                    showmessage,
                    purge, tokensource.Token);
        }

        private async Task<string> FileChooser(string title, ParsingRequest.CONTAINER container)
        {
            string extension = container == ParsingRequest.CONTAINER.C_MP4 ? ".mp4" : ".webm";
            //TODO expand this when filechoose was added
            string videopath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, Android.OS.Environment.DirectoryMovies, Grabber.EscapistDir);
            if (!Directory.Exists(videopath))
                Directory.CreateDirectory(videopath);
            return Path.Combine(videopath, title + extension);
        }

        private async void Finishhandler(string filepath, bool wascancelled)
        {
            if (!wascancelled)
            {
                var open = FindViewById<CheckBox>(Resource.Id.openchkbx).Checked;
                if (open)
                {
                    var uri = Android.Net.Uri.Parse(filepath);
                    var intent = new Intent(Intent.ActionView, uri);
                    StartActivity(intent);
                }
                else
                    await showmessage("The download is complete. The file was saved to " + filepath);
                FindViewById<EditText>(Resource.Id.urlbox).Text = "";
            }
        }

        private void Updatehandler(ulong received, ulong total)
        {
            double progress = ((double)received / total) * 100;
            var progbar = FindViewById<ProgressBar>(Resource.Id.progbar);
            progbar.Progress = (int)progress;
            progbar.Indeterminate = false;
            FindViewById<TextView>(Resource.Id.statustxt).Text = "Download running - " + (int)progress + " % ( "
                                + Grabber.ByteSize(received) + " / "
                                + Grabber.ByteSize(total) + " )";
        }

        private void purge()
        {
            //throw new NotImplementedException();
        }

        private async Task showmessage(string message)
        {
            new AlertDialog.Builder(this)
                .SetNeutralButton("OK", (a, b) => { })
                .SetMessage(message)
                .Show();
        }

        private async Task showerror(Exception e)
        {
            new AlertDialog.Builder(this)
                .SetTitle("Error")
                .SetNeutralButton("OK", (a, b) => { })
                .SetMessage(e.ToString())
                .Show();
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            FindViewById<Button>(Resource.Id.zptitlebtn).Click += async (sender, ea) =>
                new AlertDialog.Builder(this)
                .SetTitle("Current Episode")
                .SetNeutralButton("OK", (a, b) => { })
                .SetMessage(await GrabbingLib.Grabber.getLatestZPTitle())
                .Show();

            FindViewById<Button>(Resource.Id.clearbtn).Click +=
                (sender, args) => FindViewById<EditText>(Resource.Id.urlbox).Text = "";

            FindViewById<Button>(Resource.Id.pastebtn).Click +=
                async (sender, args) =>
                {
                    var cbm = (ClipboardManager)this.GetSystemService(ClipboardService);
                    if (cbm.HasPrimaryClip) //TODO query if cb content is text
                    {
                        string text = cbm.PrimaryClip.GetItemAt(0).CoerceToText(this);
                        FindViewById<EditText>(Resource.Id.urlbox).Text = text;
                        await startdl(text);
                    }
                };

            FindViewById<Button>(Resource.Id.latestzpbtn).Click += async (sender, args) =>
            {
                await startdl(Grabber.ZPLatestURL);
            };

            FindViewById<Button>(Resource.Id.awaitbtn).Click += async (sender, args) =>
            {
                var res = FindViewById<RadioButton>(Resource.Id.res360pchc).Checked
                ? ParsingRequest.RESOLUTION.R_360P
                : ParsingRequest.RESOLUTION.R_480P;
                var type = FindViewById<RadioButton>(Resource.Id.typeMP4chc).Checked
                    ? ParsingRequest.CONTAINER.C_MP4
                    : ParsingRequest.CONTAINER.C_WEBM;

                ParsingRequest request = new ParsingRequest(null, res, type);
                await Grabber.waitForNewZPEpisode(tokensource.Token,
                    async oldtitle =>
                    {
                        bool answer = false;
                        AutoResetEvent signal = new AutoResetEvent(false);
                        new AlertDialog.Builder(this).SetTitle("Confirm old episode")
                            .SetMessage("Please confirm that this is the old episode: " + oldtitle).SetNegativeButton("No", (a, b) =>
                            {
                                answer = false;
                                signal.Set();
                            }).SetPositiveButton("Yes", (a, b) =>
                            {
                                answer = true;
                                signal.Set();
                            })
                            .Show();
                        await Task.Run(() => signal.WaitOne());
                        return answer;
                    }, request, async () =>
                        {
                            await showmessage("Timeout: maximum number of attempts reached");
                            purge();
                        },
                    attempt => FindViewById<TextView>(Resource.Id.statustxt).Text = "Attempt: " + attempt, () =>
                    {
                        //No specific action
                    }, () => FindViewById<TextView>(Resource.Id.statustxt).Text = "Loading website",
                    () => FindViewById<TextView>(Resource.Id.statustxt).Text = "Loading video data", 
                    FileChooser, new Downloadhelper(Updatehandler, Finishhandler),
                    showmessage, purge, showerror);
            };

            if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.M ) { //Request permission
                string permission = Manifest.Permission.WriteExternalStorage;
                if (CheckSelfPermission(permission) != Permission.Granted)
                    RequestPermissions(new[] { permission }, permissioncode);
            }
        }
    }
}

