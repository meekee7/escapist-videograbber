using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using GrabbingLib;

// Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace UWPGrabber
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class Evaluation
    {
        private CancellationTokenSource tokensource;

        public Evaluation()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            tokensource = new CancellationTokenSource();
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                (Window.Current.Content as Frame).CanGoBack ?
                AppViewBackButtonVisibility.Visible :
                AppViewBackButtonVisibility.Collapsed;
            ProgBar.IsIndeterminate = true;
            StateLabel.Text = ResourceLoader.GetForCurrentView().GetString("StateLabel/HTMLParse");
            UserAction useraction = Appstate.state.currentaction;
            if (useraction.GetType() == typeof(GrabVideo)) //If this becomes too big then turn this into a dictionary
                await rungrabber(useraction as GrabVideo);
            else if (useraction.GetType() == typeof(GetLatestZP))
                await getvideotitle(useraction as GetLatestZP);
            else if (useraction.GetType() == typeof(WaitForNewZP))
                await waitfornewepisode(useraction as WaitForNewZP);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Grabber.finishDL();
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            tokensource.Cancel();
            var rootframe = Window.Current.Content as Frame;
            if (rootframe.CanGoBack)
            {
                e.Handled = true;
                rootframe.GoBack();
            }
        }

        private async Task waitfornewepisode(WaitForNewZP taskarguments)
        {
            ResourceLoader resload = ResourceLoader.GetForCurrentView();
            await Grabber.waitForNewZPEpisode(tokensource.Token, title =>
                CommHelp.askyesno(String.Format(resload.GetString("ConfirmTitle/Text"), title),
                    resload.GetString("ConfirmTitle/Title")), taskarguments.request,
                async () =>
                {
                    await CommHelp.showmessage(resload.GetString("TimeoutMsg"));
                    if (Frame.CanGoBack)
                        Frame.GoBack();
                }, attempt => StateLabel.Text = String.Format(resload.GetString("StateLabel/Attempt"), attempt),
                () =>
                {
                    //No specific action
                }, Htmlaction(), Jsonaction(), FileChooser(taskarguments.autosave), Downloader(taskarguments.opendl),
                CommHelp.showmessage, Canceltask(), ShowError);
        }

        private Action Canceltask()
        {
            return () =>
            {
                Grabber.finishDL();
                if (Frame.CanGoBack)
                    Frame.GoBack();
            };
        }

        private Downloadhelper Downloader(bool opendl)
        {
            ResourceLoader resload = ResourceLoader.GetForCurrentView();
            return new Downloadhelper((received, total) =>
            {
                //Progress in the download was made
                double progress = ((double)received / total) * 100;
                ProgBar.Value = progress;
                StateLabel.Text = resload.GetString("StateLabel/DLProg") + ' ' + (int)progress + " % ( "
                                  + Grabber.ByteSize(received) + " / "
                                  + Grabber.ByteSize(total) + " )";
            }, async (filepath, wascancelled) =>
            {
                if (Frame.CanGoBack) //Leaving this out causes an exception within goback
                    Frame.GoBack();
                if (!wascancelled)
                {
                    if (opendl)
                        //await Launcher.LaunchUriAsync(new Uri(filepath));
                        await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(filepath));
                    else
                        await
                            CommHelp.showmessage(String.Format(resload.GetString("dlfinish/text"), filepath,
                                resload.GetString("dlfinish/title")));
                    Grabber.finishDL();
                }
            });
        }

        private Action Jsonaction()
        {
            return () => StateLabel.Text = ResourceLoader.GetForCurrentView().GetString("StateLabel/DLStart");
        }

        private Action Htmlaction()
        {
            return () => StateLabel.Text = ResourceLoader.GetForCurrentView().GetString("StateLabel/HTMLDone");
        }

        private async Task getvideotitle(GetLatestZP taskarguments)
        {
            Task<string> titletask = Grabber.getLatestZPTitle();
            string titlestring = await titletask;
            ProgBar.IsIndeterminate = false;
            if (!tokensource.IsCancellationRequested || tokensource.Token.IsCancellationRequested)
            {
                await CommHelp.showmessage(titlestring, ResourceLoader.GetForCurrentView().GetString("Probe/title"));
                if (Frame.CanGoBack)
                    Frame.GoBack();
            }
        }

        private async Task rungrabber(GrabVideo taskarguments)
        {
            await Grabber.evaluateURL(taskarguments.request, ShowError, Htmlaction(), Jsonaction(),
                FileChooser(taskarguments.autosave), Downloader(taskarguments.opendl), CommHelp.showmessage,
                Canceltask(), tokensource.Token);
        }

        private Func<String, ParsingRequest.CONTAINER, Task<String>> FileChooser(bool autosave)
        {
            //Technically this is a case for currying, but it looks like it would make this more complicated than necessary
            return async (title, container) =>
            {
                if (autosave)
                    return await CommHelp.getAutoFilePath(title, container);
                ResourceLoader resload = ResourceLoader.GetForCurrentView();
                String extension = container == ParsingRequest.CONTAINER.C_MP4 ? ".mp4" : ".webm";
                var picker = new FileSavePicker
                {
                    DefaultFileExtension = extension,
                    SuggestedFileName = title,
                    SuggestedStartLocation = PickerLocationId.VideosLibrary
                };
                picker.FileTypeChoices.Add(
                    resload.GetString(container == ParsingRequest.CONTAINER.C_MP4 ? "FileChoiceMP4" : "FileChoiceWebM"),
                    new List<String> { extension });
                StorageFile file = await picker.PickSaveFileAsync();
                return file != null ? file.Path : null;
            };
        }

        private async Task ShowError(Exception e)
        {
            await CommHelp.showmessage(e.ToString(), ResourceLoader.GetForCurrentView().GetString("errormsg/title"));
            if (Frame.CanGoBack)
                Frame.GoBack();
        }


    }
}
