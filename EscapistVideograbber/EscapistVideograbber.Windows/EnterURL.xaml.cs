using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using EscapistVideograbber.Common;
using GrabbingLib;

// Die Elementvorlage "Standardseite" ist unter http://go.microsoft.com/fwlink/?LinkId=234237 dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    ///     Eine Standardseite mit Eigenschaften, die die meisten Anwendungen aufweisen.
    /// </summary>
    public sealed partial class EnterURL : Page
    {
        private readonly ObservableDictionary defaultViewModel = new ObservableDictionary();
        private readonly NavigationHelper navigationHelper;

        public EnterURL()
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
        private void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            if (Appstate.state.EnteredURL != null)
                URLEnterBox.Text = Appstate.state.EnteredURL;
            OpenAfterDLCB.IsChecked = Appstate.state.opendl;
            RB360P.IsChecked = Appstate.state.resolution == ParsingRequest.RESOLUTION.R_360P;
            RB480P.IsChecked = Appstate.state.resolution == ParsingRequest.RESOLUTION.R_480P;
            RBWebM.IsChecked = Appstate.state.container == ParsingRequest.CONTAINER.C_WEBM;
            RBMP4.IsChecked = Appstate.state.container == ParsingRequest.CONTAINER.C_MP4;
            AutosaveCB.IsChecked = Appstate.state.autosave;
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
            Appstate.state.EnteredURL = URLEnterBox.Text;
            Appstate.state.opendl = OpenAfterDLCB.IsChecked.HasValue && OpenAfterDLCB.IsChecked.Value;
            Appstate.state.container = RBMP4.IsChecked.GetValueOrDefault()
                ? ParsingRequest.CONTAINER.C_MP4
                : ParsingRequest.CONTAINER.C_WEBM;
            Appstate.state.resolution = RB480P.IsChecked.GetValueOrDefault()
                ? ParsingRequest.RESOLUTION.R_480P
                : ParsingRequest.RESOLUTION.R_360P;
            Appstate.state.autosave = AutosaveCB.IsChecked.HasValue && AutosaveCB.IsChecked.Value;
        }

        private void InsertZPBtn_Click(object sender, RoutedEventArgs e)
        {
            URLEnterBox.Text = Grabber.ZPLatestURL;
            startdl(); //Yeah, not beautiful that we give the DL URL to the GUI
        }

        private async void InsertClipBtn_Click(object sender, RoutedEventArgs e)
        {
            DataPackageView cbcontent = Clipboard.GetContent();
            if (cbcontent.Contains(StandardDataFormats.Text))
                URLEnterBox.Text = await cbcontent.GetTextAsync();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            startdl();
        }

        private void URLEnterBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                startdl();
        }

        private void startdl()
        {
            Appstate.state.currentaction = new GrabVideo(new ParsingRequest(URLEnterBox.Text,
                            Appstate.state.resolution =
                                RB480P.IsChecked.GetValueOrDefault()
                                    ? ParsingRequest.RESOLUTION.R_480P
                                    : ParsingRequest.RESOLUTION.R_360P,
                            RBMP4.IsChecked.GetValueOrDefault()
                                ? ParsingRequest.CONTAINER.C_MP4
                                : ParsingRequest.CONTAINER.C_WEBM), OpenAfterDLCB.IsChecked.GetValueOrDefault(), AutosaveCB.IsChecked.GetValueOrDefault());
            Frame.Navigate(typeof (Evaluation));
        }
        
        private void ProbeBtn_Click(object sender, RoutedEventArgs e)
        {
            Appstate.state.currentaction = new GetLatestZP();
            Frame.Navigate(typeof (Evaluation));
        }

        private void WaitZPBtn_Click(object sender, RoutedEventArgs e)
        {
            Appstate.state.currentaction = new WaitForNewZP(new ParsingRequest(Grabber.ZPLatestURL,
                            Appstate.state.resolution =
                                RB480P.IsChecked.GetValueOrDefault()
                                    ? ParsingRequest.RESOLUTION.R_480P
                                    : ParsingRequest.RESOLUTION.R_360P,
                            RBMP4.IsChecked.GetValueOrDefault()
                                ? ParsingRequest.CONTAINER.C_MP4
                                : ParsingRequest.CONTAINER.C_WEBM), OpenAfterDLCB.IsChecked.GetValueOrDefault(),
                AutosaveCB.IsChecked.GetValueOrDefault());
            Frame.Navigate(typeof (Evaluation));
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