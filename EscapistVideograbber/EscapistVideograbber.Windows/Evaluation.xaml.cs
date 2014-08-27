using EscapistVideograbber.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

// Die Elementvorlage "Standardseite" ist unter http://go.microsoft.com/fwlink/?LinkId=234237 dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    /// Eine Standardseite mit Eigenschaften, die die meisten Anwendungen aufweisen.
    /// </summary>
    public sealed partial class Evaluation : Page
    {

        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        /// <summary>
        /// Dies kann in ein stark typisiertes Anzeigemodell geändert werden.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// NavigationHelper wird auf jeder Seite zur Unterstützung bei der Navigation verwendet und 
        /// Verwaltung der Prozesslebensdauer
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }


        public Evaluation()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
            this.navigationHelper.SaveState += navigationHelper_SaveState;
        }

        /// <summary>
        /// Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird. Gespeicherte Zustände werden ebenfalls
        /// bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
        /// </summary>
        /// <param name="sender">
        /// Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Ereignisdaten, die die Navigationsparameter bereitstellen, die an
        /// <see cref="Frame.Navigate(Type, Object)"/> als diese Seite ursprünglich angefordert wurde und
        /// ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
        /// beibehalten wurde. Der Zustand ist beim ersten Aufrufen einer Seite NULL.</param>
        private async void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            this.ProgBar.IsIndeterminate = true;
            ResourceLoader resload = new ResourceLoader();
            StateLabel.Text = resload.GetString("StateLabel/HTMLParse");
            await Grabber.evaluateURL(Appstate.state.EnteredURL, this.ShowError, () =>
            { //HTML parsed
                StateLabel.Text = resload.GetString("StateLabel/HTMLDone");
            }, () =>
            { //JSON parsed
                StateLabel.Text = resload.GetString("StateLabel/DLStart");
            }, this.FileChooser, new Downloadhelper(), async (ulong received, ulong total, String filepath) =>
            { //Progress in the download was made
                double progress = ((double) received/ total) * 100;
                ProgBar.Value = progress;
                StateLabel.Text = resload.GetString("StateLabel/DLProg") + ' ' + (int) progress + " % ( "
                    + Grabber.ByteSize(received) + " / "
                    + Grabber.ByteSize(total) + " )";
                if (received == total)
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
                    this.Frame.GoBack();
                }
            },this.showmsg);
        }

        /// <summary>
        /// Behält den dieser Seite zugeordneten Zustand bei, wenn die Anwendung angehalten oder
        /// die Seite im Navigationscache verworfen wird.  Die Werte müssen den Serialisierungsanforderungen
        /// von <see cref="SuspensionManager.SessionState"/> entsprechen.
        /// </summary>
        /// <param name="sender">Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper"/></param>
        /// <param name="e">Ereignisdaten, die ein leeres Wörterbuch zum Auffüllen bereitstellen
        /// serialisierbarer Zustand.</param>
        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }

        #region NavigationHelper-Registrierung

        /// Die in diesem Abschnitt bereitgestellten Methoden werden einfach verwendet, um
        /// damit NavigationHelper auf die Navigationsmethoden der Seite reagieren kann.
        /// 
        /// Platzieren Sie seitenspezifische Logik in Ereignishandlern für  
        /// <see cref="GridCS.Common.NavigationHelper.LoadState"/>
        /// und <see cref="GridCS.Common.NavigationHelper.SaveState"/>.
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

        private void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            Grabber.finishDL();
        }

        private async Task<String> FileChooser(String title)
        {
            ResourceLoader resload = new ResourceLoader();
            FileSavePicker picker = new FileSavePicker();
            picker.DefaultFileExtension = ".mp4";
            picker.FileTypeChoices.Add(resload.GetString("FileChoiceMP4"), new List<String>() { ".mp4" });
            picker.SuggestedFileName = title;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            return (await picker.PickSaveFileAsync()).Path;
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
        private async Task showmsg(String msg)
        {
            ResourceLoader resload = new ResourceLoader();
            MessageDialog dialog = new MessageDialog(msg, "ShowMSG");
            dialog.Commands.Add(new UICommand(resload.GetString("dlfinish/ok")));
            await dialog.ShowAsync();
        }
 
    }
}
