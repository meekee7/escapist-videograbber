using EscapistVideograbber.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using System.Threading.Tasks;
using GrabbingLib;
using Windows.UI.Popups;
using Windows.ApplicationModel.Resources;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Threading;

// Die Elementvorlage "Standardseite" ist unter "http://go.microsoft.com/fwlink/?LinkID=390556" dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet werden kann oder auf die innerhalb eines Frames navigiert werden kann.
    /// </summary>
    public sealed partial class Evaluation : Page, IFileSavePickerContinuable
    {
        public static Page thispage { get; private set; }
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        private CancellationTokenSource tokensource;
        AutoResetEvent signalevt = new AutoResetEvent(false);
        public volatile String filepath = null; //We are passing data between threads through a global variable. Quick and very dirty

        public Evaluation()
        {
            thispage = this;

            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;
        }

        /// <summary>
        /// Ruft den <see cref="NavigationHelper"/> ab, der mit dieser <see cref="Page"/> verknüpft ist.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        /// <summary>
        /// Ruft das Anzeigemodell für diese <see cref="Page"/> ab.
        /// Dies kann in ein stark typisiertes Anzeigemodell geändert werden.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird.  Gespeicherte Zustände werden ebenfalls
        /// bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
        /// </summary>
        /// <param name="sender">
        /// Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Ereignisdaten, die die Navigationsparameter bereitstellen, die an
        /// <see cref="Frame.Navigate(Type, Object)"/> als diese Seite ursprünglich angefordert wurde und
        /// ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
        /// beibehalten wurde.  Der Zustand ist beim ersten Aufrufen einer Seite NULL.</param>
        private async void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            Windows.Phone.UI.Input.HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            this.tokensource = new CancellationTokenSource();
            this.ProgBar.IsIndeterminate = true;
            ResourceLoader resload = new ResourceLoader();
            StateLabel.Text = resload.GetString("StateLabel/HTMLParse");
            await Grabber.evaluateURL(Appstate.state.EnteredURL, Appstate.state.hq, this.ShowError, () =>
            { //HTML parsed
                StateLabel.Text = resload.GetString("StateLabel/HTMLDone");
            }, () =>
            { //JSON parsed
                StateLabel.Text = resload.GetString("StateLabel/DLStart");
            }, this.FileChooser, new Downloadhelper(async (ulong received, ulong total) =>
            { //Progress in the download was made
                double progress = ((double) received / total) * 100;
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
            }, async (String filepath, bool wascancelled) =>
            {//Finish is integrated into progress
                if (wascancelled)
                    this.Frame.GoBack(); //TODO
                else
                {
                    if (Appstate.state.opendl)
                        await Windows.System.Launcher.LaunchFileAsync(await Windows.Storage.StorageFile.GetFileFromPathAsync(filepath));
                    else
                    {
                        MessageDialog dialog = new MessageDialog(resload.GetString("dlfinish/text"), resload.GetString("dlfinish/title"));
                        dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
                        await dialog.ShowAsync();
                    }
                    Grabber.finishDL();
                }
            }), this.showmsg, () =>
            {
                GrabbingLib.Grabber.finishDL();
                if (navigationHelper.CanGoBack())
                    navigationHelper.GoBack();
            }, this.tokensource.Token);
        }

        void HardwareButtons_BackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            this.tokensource.Cancel();
        }

        /// <summary>
        /// Behält den dieser Seite zugeordneten Zustand bei, wenn die Anwendung angehalten oder
        /// die Seite im Navigationscache verworfen wird.  Die Werte müssen den Serialisierungsanforderungen
        /// von <see cref="SuspensionManager.SessionState"/> entsprechen.
        /// </summary>
        /// <param name="sender">Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/></param>
        /// <param name="e">Ereignisdaten, die ein leeres Wörterbuch zum Auffüllen bereitstellen
        /// serialisierbarer Zustand.</param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            Windows.Phone.UI.Input.HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
        }

        /*
         * private async Task<String> FileChooser(String title)
        {
            /*ResourceLoader resload = new ResourceLoader();
            FileSavePicker picker = new FileSavePicker();
            picker.DefaultFileExtension = ".mp4";
            picker.FileTypeChoices.Add(resload.GetString("FileChoiceMP4"), new List<String>() { ".mp4" });
            picker.SuggestedFileName = title;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.PickSaveFileAndContinue();
            String filename = title + ".mp4";
            String path = null;

            StorageFile file = (await Windows.Storage.KnownFolders.VideosLibrary.GetFilesAsync()).FirstOrDefault(x => x.Name.Equals(filename));
            if (file == null)
            {
                file = await KnownFolders.VideosLibrary.CreateFileAsync(filename);
                path = file.Path;
            }
            else
            {
                ResourceLoader resload = new ResourceLoader();
                MessageDialog msgdialog = new MessageDialog(resload.GetString("overwritemsg/body"), resload.GetString("overwritemsg/title"));
                msgdialog.Commands.Add(new UICommand(resload.GetString("overwritemsg/yes"), (IUICommand command) =>
                {
                    path = file.Path;
                }));
                msgdialog.Commands.Add(new UICommand(resload.GetString("overwritemsg/no"), (IUICommand command) =>
                {
                    path = null;
                }));
                await msgdialog.ShowAsync();
            }
            //await showmsg(path);
            return path;
            //return (await picker.).Path;
        }*/

        public void ContinueFileSavePicker(Windows.ApplicationModel.Activation.FileSavePickerContinuationEventArgs args)
        {
            filepath = args != null && args.File != null && args.File.Path != null ? args.File.Path : null;
            signalevt.Set();
        }

        private async Task<String> FileChooser(String title)
        {
            if (Appstate.state.autosave)
                return await CommHelp.getAutoFilePath(title);
            else
            {
                Task waitforresult = Task.Factory.StartNew(() =>
                {
                    signalevt.WaitOne();
                });

                Task choosertask = Task.Factory.StartNew(async () =>
                {
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        ResourceLoader resload = new ResourceLoader();
                        FileSavePicker picker = new FileSavePicker();
                        picker.DefaultFileExtension = ".mp4";
                        picker.FileTypeChoices.Add(resload.GetString("FileChoiceMP4"), new List<String>() { ".mp4" });
                        picker.SuggestedFileName = title;
                        picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
                        picker.PickSaveFileAndContinue();
                    });
                });
                //waitforresult.Start();
                //choosertask.Start();

                await waitforresult;

                return filepath;
            }
            /*String filename = title + ".mp4";
            String path = null;

            StorageFile file = (await Windows.Storage.KnownFolders.VideosLibrary.GetFilesAsync()).FirstOrDefault(x => x.Name.Equals(filename));
            if (file == null)
            {
                file = await KnownFolders.VideosLibrary.CreateFileAsync(filename);
                path = file.Path;
            }
            else
            {
                ResourceLoader resload = new ResourceLoader();
                MessageDialog msgdialog = new MessageDialog(resload.GetString("overwritemsg/body"), resload.GetString("overwritemsg/title"));
                msgdialog.Commands.Add(new UICommand(resload.GetString("overwritemsg/yes"), (IUICommand command) =>
                {
                    path = file.Path;
                }));
                msgdialog.Commands.Add(new UICommand(resload.GetString("overwritemsg/no"), (IUICommand command) =>
                {
                    path = null;
                }));
                await msgdialog.ShowAsync();
            }
            //await showmsg(path);
            return path;*/
            //return (await picker.).Path;
        }

        private async Task ShowError(Exception e)
        {
            ResourceLoader resload = new ResourceLoader();
            MessageDialog msgdialog = new MessageDialog(e.ToString(), resload.GetString("errormsg/title"));
            msgdialog.Commands.Add(new UICommand(resload.GetString("errormsg/ok"), (IUICommand command) =>
            {
                try
                {
                    this.Frame.GoBack();
                }
                catch (Exception)
                {
                }
            }));
            await msgdialog.ShowAsync();
        }

        #region NavigationHelper-Registrierung

        /// <summary>
        /// Die in diesem Abschnitt bereitgestellten Methoden werden einfach verwendet, um
        /// damit NavigationHelper auf die Navigationsmethoden der Seite reagieren kann.
        /// <para>
        /// Platzieren Sie seitenspezifische Logik in Ereignishandlern für  
        /// <see cref="NavigationHelper.LoadState"/>
        /// und <see cref="NavigationHelper.SaveState"/>.
        /// Der Navigationsparameter ist in der LoadState-Methode verfügbar 
        /// zusätzlich zum Seitenzustand, der während einer früheren Sitzung beibehalten wurde.
        /// </para>
        /// </summary>
        /// <param name="e">Stellt Daten für Navigationsmethoden und -ereignisse bereit.
        /// Handler, bei denen die Navigationsanforderung nicht abgebrochen werden kann.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Grabber.finishDL();
        }

        private async Task showmsg(String msg)
        {
            ResourceLoader resload = new ResourceLoader();
            MessageDialog dialog = new MessageDialog(msg, "ShowMSG");
            dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
            await dialog.ShowAsync();
        }
    }
}
