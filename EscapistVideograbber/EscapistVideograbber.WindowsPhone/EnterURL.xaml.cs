//using Microsoft.Phone.Net.NetworkInformation;

using System;
using Windows.ApplicationModel.Resources;
using Windows.Networking.Connectivity;
using Windows.Phone.UI.Input;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using EscapistVideograbber.Common;
using GrabbingLib;

// Die Elementvorlage "Standardseite" ist unter "http://go.microsoft.com/fwlink/?LinkID=390556" dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    ///     Eine leere Seite, die eigenständig verwendet werden kann oder auf die innerhalb eines Frames navigiert werden kann.
    /// </summary>
    public sealed partial class EnterURL : Page
    {
        private readonly ObservableDictionary defaultViewModel = new ObservableDictionary();
        private readonly NavigationHelper navigationHelper;

        private bool overrideNOWIFI;

        public EnterURL()
        {
            InitializeComponent();
            navigationHelper = new NavigationHelper(this);
            navigationHelper.LoadState += NavigationHelper_LoadState;
            navigationHelper.SaveState += NavigationHelper_SaveState;
        }

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

        /// <summary>
        ///     Füllt die Seite mit Inhalt auf, der bei der Navigation übergeben wird.  Gespeicherte Zustände werden ebenfalls
        ///     bereitgestellt, wenn eine Seite aus einer vorherigen Sitzung neu erstellt wird.
        /// </summary>
        /// <param name="sender">
        ///     Die Quelle des Ereignisses, normalerweise <see cref="NavigationHelper" />
        /// </param>
        /// <param name="e">
        ///     Ereignisdaten, die die Navigationsparameter bereitstellen, die an
        ///     <see cref="Frame.Navigate(Type, Object)" /> als diese Seite ursprünglich angefordert wurde und
        ///     ein Wörterbuch des Zustands, der von dieser Seite während einer früheren
        ///     beibehalten wurde.  Der Zustand ist beim ersten Aufrufen einer Seite NULL.
        /// </param>
        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            if (Appstate.state.EnteredURL != null)
                URLBox.Text = Appstate.state.EnteredURL;
            OpenAfterDLCB.IsChecked = Appstate.state.opendl;
            AutosaveCB.IsChecked = Appstate.state.autosave;
            HQCB.IsChecked = Appstate.state.hq;
            overrideNOWIFI = false;

            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            Application.Current.Exit();
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
            Appstate.state.EnteredURL = URLBox.Text;
            Appstate.state.opendl = OpenAfterDLCB.IsChecked.HasValue && OpenAfterDLCB.IsChecked.Value;
            Appstate.state.hq = HQCB.IsChecked.HasValue && HQCB.IsChecked.Value;
            Appstate.state.autosave = AutosaveCB.IsChecked.HasValue && AutosaveCB.IsChecked.Value;
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
        }

        private void InsertZPBtn_Click(object sender, RoutedEventArgs e)
        {
            URLBox.Text = Grabber.ZPLatestURL;
            startdl(); //Yeah, not beautiful that we give the DL URL to the GUI
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            startdl();
        }

        private void URLBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                startdl();
        }

        private async void startdl()
        {
            //IFF the connection is not mobile, only wifi (or manual override)
            //if (DeviceNetworkInformation.IsWiFiEnabled && !DeviceNetworkInformation.IsCellularDataEnabled && DeviceNetworkInformation.IsNetworkAvailable)
            if (overrideNOWIFI || !NetworkInformation.GetInternetConnectionProfile().IsWwanConnectionProfile)
            {
                Appstate.state.currentaction = new GrabVideo(URLBox.Text, OpenAfterDLCB.IsChecked.GetValueOrDefault(),
                    HQCB.IsChecked.GetValueOrDefault(), AutosaveCB.IsChecked.GetValueOrDefault());
                Frame.Navigate(typeof (Evaluation));
            }
            else
                await CommHelp.showmessage(ResourceLoader.GetForCurrentView().GetString("NoWIFImsg/text"));
        }

        private void clearbtn_Click(object sender, RoutedEventArgs e)
        {
            URLBox.Text = String.Empty;
        }

        private async void OverrideNOWIFIbtn_Click(object sender, RoutedEventArgs e)
        {
            ResourceLoader resload = ResourceLoader.GetForCurrentView();
            var msgdialog = new MessageDialog(resload.GetString("OverrideWIFImsg/text"),
                resload.GetString("OverrideWIFImsg/title"));
            msgdialog.Commands.Add(new UICommand(resload.GetString("OverrideWIFImsg/yes"),
                command => { overrideNOWIFI = true; }));
            msgdialog.Commands.Add(new UICommand(resload.GetString("OverrideWIFImsg/no"),
                command => { overrideNOWIFI = false; }));
            await msgdialog.ShowAsync();
        }

        private void ProbeBtn_Click(object sender, RoutedEventArgs e)
        {
            Appstate.state.currentaction = new GetLatestZP();
            Frame.Navigate(typeof (Evaluation));
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