﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using EscapistVideograbber.Common;
using GrabbingLib;

// Die Elementvorlage "Standardseite" ist unter http://go.microsoft.com/fwlink/?LinkId=234237 dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    ///     Eine Standardseite mit Eigenschaften, die die meisten Anwendungen aufweisen.
    /// </summary>
    public sealed partial class Evaluation : Page
    {
        private readonly ObservableDictionary defaultViewModel = new ObservableDictionary();
        private readonly NavigationHelper navigationHelper;

        private CancellationTokenSource tokensource;

        public Evaluation()
        {
            InitializeComponent();
            navigationHelper = new NavigationHelper(this);
            navigationHelper.LoadState += navigationHelper_LoadState;
            navigationHelper.SaveState += navigationHelper_SaveState;
        }

        /// <summary>
        ///     Dies kann in ein stark typisiertes Anzeigemodell geändert werden.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return defaultViewModel; }
        }

        /// <summary>
        ///     NavigationHelper wird auf jeder Seite zur Unterstützung bei der Navigation verwendet und
        ///     Verwaltung der Prozesslebensdauer
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return navigationHelper; }
        }


        /// <summary>
        ///     Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird. Gespeicherte Zustände werden ebenfalls
        ///     bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
        /// </summary>
        /// <param name="sender">
        ///     Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper" />
        /// </param>
        /// <param name="e">
        ///     Ereignisdaten, die die Navigationsparameter bereitstellen, die an
        ///     <see cref="Frame.Navigate(Type, Object)" /> als diese Seite ursprünglich angefordert wurde und
        ///     ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
        ///     beibehalten wurde. Der Zustand ist beim ersten Aufrufen einer Seite NULL.
        /// </param>
        private async void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            tokensource = new CancellationTokenSource();
            backButton.Click += backButton_Click;
            ProgBar.IsIndeterminate = true;
            StateLabel.Text = ResourceLoader.GetForCurrentView().GetString("StateLabel/HTMLParse");
            UserAction useraction = Appstate.state.currentaction;
            if (useraction.GetType() == typeof (GrabVideo)) //If this becomes too big then turn this into a dictionary
                await rungrabber(useraction as GrabVideo);
            else if (useraction.GetType() == typeof (GetLatestZP))
                await getvideotitle(useraction as GetLatestZP);
            else if (useraction.GetType() == typeof (WaitForNewZP))
                await waitfornewepisode(useraction as WaitForNewZP);
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
                double progress = ((double) received/total)*100;
                ProgBar.Value = progress;
                StateLabel.Text = resload.GetString("StateLabel/DLProg") + ' ' + (int) progress + " % ( "
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

        private void backButton_Click(object sender, RoutedEventArgs e)
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
        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            backButton.Click -= backButton_Click;
        }

        private void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            Grabber.finishDL();
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
                    new List<String> {extension});
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

        #region NavigationHelper-Registrierung

        /// Die in diesem Abschnitt bereitgestellten Methoden werden einfach verwendet, um
        /// damit NavigationHelper auf die Navigationsmethoden der Seite reagieren kann.
        /// 
        /// Platzieren Sie seitenspezifische Logik in Ereignishandlern für
        /// <see cref="GridCS.Common.NavigationHelper.LoadState" />
        /// und
        /// <see cref="GridCS.Common.NavigationHelper.SaveState" />
        /// .
        /// Der Navigationsparameter ist in der LoadState-Methode verfügbar 
        /// zusätzlich zum Seitenzustand, der während einer früheren Sitzung beibehalten wurde.
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