using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Popups;
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
            var resload = ResourceLoader.GetForCurrentView();
            StateLabel.Text = resload.GetString("StateLabel/HTMLParse");
            await Grabber.evaluateURL(Appstate.state.EnteredURL, Appstate.state.hq, ShowError, () =>
            {
                //HTML parsed
                StateLabel.Text = resload.GetString("StateLabel/HTMLDone");
            }, () =>
            {
                //JSON parsed
                StateLabel.Text = resload.GetString("StateLabel/DLStart");
            }, FileChooser, new Downloadhelper((received, total) =>
            {
                //Progress in the download was made
                double progress = ((double) received/total)*100;
                ProgBar.Value = progress;
                StateLabel.Text = resload.GetString("StateLabel/DLProg") + ' ' + (int) progress + " % ( "
                                  + Grabber.ByteSize(received) + " / "
                                  + Grabber.ByteSize(total) + " )";
                /*if (received == total)
                {
                    if (Appstate.state.opendl)
                        await Windows.System.Launcher.LaunchFileAsync(await Windows.Storage.StorageFile.GetFileFromPathAsync(""));
                    else
                    {
                        MessageDialog dialog = new MessageDialog(resload.GetString("dlfinish/text"), resload.GetString("dlfinish/title"));
                        dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
                        await dialog.ShowAsync();
                    }
                    Grabber.finishDL();
                    this.Frame.GoBack();
                }*/
            }, async (filepath, wascancelled) =>
            {
                //Finish is integrated into progress
                if (wascancelled)
                    Frame.GoBack(); //TODO
                else
                {
                    if (Appstate.state.opendl)
                        //await Launcher.LaunchUriAsync(new Uri("video:" + filepath));
                        await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(filepath));
                    else
                    {
                        var dialog = new MessageDialog(String.Format(resload.GetString("dlfinish/text"), filepath),
                            resload.GetString("dlfinish/title"));
                        dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
                        await dialog.ShowAsync();
                    }
                    Grabber.finishDL();
                }
            }), showmsg, () =>
            {
                Grabber.finishDL();
                if (navigationHelper.CanGoBack())
                    navigationHelper.GoBack();
            }, tokensource.Token);
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

        private async Task<String> FileChooser(String title)
        {
            if (Appstate.state.autosave)
                return await CommHelp.getAutoFilePath(title);
            var resload = new ResourceLoader();
            var picker = new FileSavePicker
            {
                DefaultFileExtension = ".mp4",
                SuggestedFileName = title,
                SuggestedStartLocation = PickerLocationId.VideosLibrary
            };
            picker.FileTypeChoices.Add(resload.GetString("FileChoiceMP4"), new List<String> {".mp4"});
            StorageFile file = await picker.PickSaveFileAsync();
            return file != null ? file.Path : null;
        }

        private async Task ShowError(Exception e)
        {
            var resload = new ResourceLoader();
            var msgdialog = new MessageDialog(e.ToString(), resload.GetString("errormsg/title"));
            msgdialog.Commands.Add(new UICommand(resload.GetString("errormsg/ok"), command =>
            {
                try
                {
                    Frame.GoBack();
                }
                catch (Exception)
                {
                }
            }));
            await msgdialog.ShowAsync();
        }

        private async Task showmsg(String msg)
        {
            var resload = new ResourceLoader();
            var dialog = new MessageDialog(msg, "ShowMSG");
            dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
            await dialog.ShowAsync();
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