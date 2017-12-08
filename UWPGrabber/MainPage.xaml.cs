using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

// Die Vorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 dokumentiert.

namespace UWPGrabber
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
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
            Frame.Navigate(typeof(Evaluation));
        }

        private void ProbeBtn_Click(object sender, RoutedEventArgs e)
        {
            Appstate.state.currentaction = new GetLatestZP();
            Frame.Navigate(typeof(Evaluation));
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
            Frame.Navigate(typeof(Evaluation));
        }
    }
}
