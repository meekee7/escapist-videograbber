using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Phone.Devices.Notification;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using EscapistVideograbber.Common;
using GrabbingLib;

// Die Elementvorlage "Standardseite" ist unter "http://go.microsoft.com/fwlink/?LinkID=390556" dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    ///     Eine leere Seite, die eigenständig verwendet werden kann oder auf die innerhalb eines Frames navigiert werden kann.
    /// </summary>
    public sealed partial class Evaluation : Page, IFileSavePickerContinuable
    {
        private readonly ObservableDictionary defaultViewModel = new ObservableDictionary();
        private readonly NavigationHelper navigationHelper;

        private readonly AutoResetEvent signalevt = new AutoResetEvent(false);

        public volatile String filepath = null;
        //We are passing data between threads through a global variable. Quick and very dirty

        private CancellationTokenSource tokensource;

        public Evaluation()
        {
            thispage = this;

            InitializeComponent();

            navigationHelper = new NavigationHelper(this);
            navigationHelper.LoadState += NavigationHelper_LoadState;
            navigationHelper.SaveState += NavigationHelper_SaveState;
        }

        public static Page thispage { get; private set; }

        /// <summary>
        ///     Ruft den <see cref="NavigationHelper" /> ab, der mit dieser <see cref="Page" /> verknüpft ist.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return navigationHelper; }
        }

        /// <summary>
        ///     Ruft das Anzeigemodell für diese <see cref="Page" /> ab.
        ///     Dies kann in ein stark typisiertes Anzeigemodell geändert werden.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return defaultViewModel; }
        }

        public void ContinueFileSavePicker(FileSavePickerContinuationEventArgs args)
        {
            filepath = args != null && args.File != null ? args.File.Path : null;
            signalevt.Set();
        }

        /// <summary>
        ///     Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird.  Gespeicherte Zustände werden ebenfalls
        ///     bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
        /// </summary>
        /// <param name="sender">
        ///     Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper" />
        /// </param>
        /// <param name="e">
        ///     Ereignisdaten, die die Navigationsparameter bereitstellen, die an
        ///     <see cref="navigationHelper.Navigate(Type, Object)" /> als diese Seite ursprünglich angefordert wurde und
        ///     ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
        ///     beibehalten wurde.  Der Zustand ist beim ersten Aufrufen einer Seite NULL.
        /// </param>
        private async void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            tokensource = new CancellationTokenSource();
            ProgBar.IsIndeterminate = true;
            StateLabel.Text = ResourceLoader.GetForCurrentView().GetString("StateLabel/HTMLParse");
            var useraction = Appstate.state.currentaction;
            if (useraction.GetType() == typeof (GrabVideo)) //If this becomes too big then turn this into a dictionary
                await rungrabber(useraction as GrabVideo);
            else if (useraction.GetType() == typeof (GetLatestZP))
                await getvideotitle(useraction as GetLatestZP);
            else if (useraction.GetType() == typeof (WaitForNewZP))
                await waitfornewepisode(useraction as WaitForNewZP);
        }

        private async Task waitfornewepisode(WaitForNewZP taskarguments)
        {
            var resload = ResourceLoader.GetForCurrentView();
            StateLabel.Text = resload.GetString("StateLabel/HTMLParse");
            await
                Grabber.waitForNewZPEpisode(tokensource.Token,
                    title => CommHelp.askyesno(String.Format(resload.GetString("ConfirmTitle/Text"), title),
                        resload.GetString("ConfirmTitle/Title")), async () =>
                        {
                            await CommHelp.showmessage(resload.GetString("TimeoutMsg"));
                            if (Frame.CanGoBack)
                                Frame.GoBack();
                        }, attempt => StateLabel.Text = String.Format(resload.GetString("StateLabel/Attempt"), attempt),
                    () => VibrationDevice.GetDefault().Vibrate(TimeSpan.FromSeconds(0.2)),
                    Htmlaction(), Jsonaction(), getfilechooser(taskarguments.autosave),
                    Downloader(taskarguments.opendl), CommHelp.showmessage, Canceltask(), ShowError);
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
                double progress = ((double) received/total)*100;
                ProgBar.Value = progress;
                StateLabel.Text = resload.GetString("StateLabel/DLProg") + ' ' + (int) progress + " % ( "
                                  + Grabber.ByteSize(received) + " / "
                                  + Grabber.ByteSize(total) + " )";
            }, async (filepath, wascancelled) =>
            {
                //Finish is integrated into progress
                if (Frame.CanGoBack)
                    Frame.GoBack();
                if (!wascancelled)
                {
                    if (opendl)
                    {
                        //await Launcher.LaunchUriAsync(new Uri(filepath));
                        //await Launcher.LaunchUriAsync(new Uri("ms-appx:///" + filepath));
                        //TODO see if there is a better way, maybe launch the video app directly
                        await Task.Delay(1000); //Seems to avoid the video not loading if we wait a little
                        await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(filepath));
                        await Task.Delay(1000);
                    }
                    else
                    {
                        var dialog = new MessageDialog(String.Format(resload.GetString("dlfinish/text"), filepath),
                            resload.GetString("dlfinish/title"));
                        dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
                        VibrationDevice.GetDefault().Vibrate(TimeSpan.FromSeconds(0.2));
                        await dialog.ShowAsync();
                    }
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
            var titlestring = await Grabber.getLatestZPTitle();
            if (!tokensource.IsCancellationRequested && !tokensource.Token.IsCancellationRequested)
            {
                await CommHelp.showmessage(titlestring, ResourceLoader.GetForCurrentView().GetString("Probe/title"));
                if (Frame.CanGoBack)
                    Frame.GoBack();
            }
        }

        private async Task rungrabber(GrabVideo taskarguments)
        {
            await Grabber.evaluateURL(taskarguments.request, ShowError, Htmlaction(), Jsonaction(),
                getfilechooser(taskarguments.autosave), Downloader(taskarguments.opendl), CommHelp.showmessage,
                Canceltask(), tokensource.Token);
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            tokensource.Cancel();
        }

        /// <summary>
        ///     Behält den dieser Seite zugeordneten Zustand bei, wenn die Anwendung angehalten oder
        ///     die Seite im Navigationscache verworfen wird.  Die Werte müssen den Serialisierungsanforderungen
        ///     von <see cref="SuspensionManager.SessionState" /> entsprechen.
        /// </summary>
        /// <param name="sender">Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper" /></param>
        /// <param name="e">
        ///     Ereignisdaten, die ein leeres Wörterbuch zum Auffüllen bereitstellen
        ///     serialisierbarer Zustand.
        /// </param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
        }

        private Func<String, ParsingRequest.CONTAINER, Task<String>> getfilechooser(bool autosave)
        {
            return async (title, container) =>
            {
                if (autosave)
                    return await CommHelp.getAutoFilePath(title, container);

                Task waitforresult = Task.Factory.StartNew(() => signalevt.WaitOne());
                //Extra task so we can await it

                await Task.Factory.StartNew(async () =>
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        String extension = container == ParsingRequest.CONTAINER.C_MP4 ? ".mp4" : ".webm";
                        var resload = ResourceLoader.GetForCurrentView();
                        var picker = new FileSavePicker {DefaultFileExtension = extension};
                        picker.FileTypeChoices.Add(resload.GetString(container == ParsingRequest.CONTAINER.C_MP4 ? "FileChoiceMP4" : "FileChoiceWebM"), new List<String> {extension});
                        picker.SuggestedFileName = title;
                        picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
                        picker.PickSaveFileAndContinue(); //And this function just exits your thread and never returns
                    })
                    //which explains the construct with the signal and the interface implementation, where we get the results
                    );

                await waitforresult; //Wait for the signal thread

                return this.filepath;
            };
        }

        private async Task ShowError(Exception e)
        {
            await CommHelp.showmessage(e.ToString(), ResourceLoader.GetForCurrentView().GetString("errormsg/title"));
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Grabber.finishDL();
        }

        #region NavigationHelper-Registrierung

        /// <summary>
        ///     Die in diesem Abschnitt bereitgestellten Methoden werden einfach verwendet, um
        ///     damit NavigationHelper auf die Navigationsmethoden der Seite reagieren kann.
        ///     <para>
        ///         Platzieren Sie seitenspezifische Logik in Ereignishandlern für
        ///         <see cref="NavigationHelper.LoadState" />
        ///         und <see cref="NavigationHelper.SaveState" />.
        ///         Der Navigationsparameter ist in der LoadState-Methode verfügbar
        ///         zusätzlich zum Seitenzustand, der während einer früheren Sitzung beibehalten wurde.
        ///     </para>
        /// </summary>
        /// <param name="e">
        ///     Stellt Daten für Navigationsmethoden und -ereignisse bereit.
        ///     Handler, bei denen die Navigationsanforderung nicht abgebrochen werden kann.
        /// </param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);
        }

        #endregion
    }
}