using System;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using EscapistVideograbber.Common;

// Die Projektvorlage "Universelle Hub-Anwendung" ist unter http://go.microsoft.com/fwlink/?LinkID=391955 dokumentiert.

namespace EscapistVideograbber
{
    /// <summary>
    ///     Stellt das anwendungsspezifische Verhalten bereit, um die Standardanwendungsklasse zu ergänzen.
    /// </summary>
    public sealed partial class App : Application
    {
#if WINDOWS_PHONE_APP
        private TransitionCollection transitions;

        private ContinuationManager continuationManager;
#endif

        /// <summary>
        ///     Initialisiert die Singletoninstanz der <see cref="App" />-Klasse. Dies ist die erste ausgeführte Zeile von
        ///     erstelltem Code
        ///     und daher das logische Äquivalent von main() bzw. WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        ///     Handle OnActivated event to deal with File Open/Save continuation activation kinds
        /// </summary>
        /// <param name="e">Application activated event arguments, it can be casted to proper sub-type based on ActivationKind</param>
        protected override async void OnActivated(IActivatedEventArgs e)
        {
            base.OnActivated(e);

            continuationManager = new ContinuationManager();
            continuationManager.Continue(e as IContinuationActivatedEventArgs, Evaluation.thispage.Content as Frame);

            Window.Current.Activate();
        }
#endif

        /// <summary>
        ///     Wird aufgerufen, wenn die Anwendung durch den Endbenutzer normal gestartet wird.  Weitere Einstiegspunkte
        ///     werden verwendet, wenn die Anwendung zum Öffnen einer bestimmten Datei, zum Anzeigen
        ///     von Suchergebnissen usw. gestartet wird.
        /// </summary>
        /// <param name="e">Details über Startanforderung und -prozess.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (Debugger.IsAttached)
            {
                DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            var rootFrame = Window.Current.Content as Frame;

            // App-Initialisierung nicht wiederholen, wenn das Fenster bereits Inhalte enthält.
            // Nur sicherstellen, dass das Fenster aktiv ist.
            if (rootFrame == null)
            {
                // Einen Rahmen erstellen, der als Navigationskontext fungiert und zum Parameter der ersten Seite navigieren
                rootFrame = new Frame();

                //Den Frame mit einem SuspensionManager-Schlüssel verknüpfen                                
                SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

                // TODO: diesen Wert auf eine Cachegröße ändern, die für Ihre Anwendung geeignet ist
                rootFrame.CacheSize = 1;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // Den gespeicherten Sitzungszustand nur bei Bedarf wiederherstellen
                    try
                    {
                        await SuspensionManager.RestoreAsync();
                    }
                    catch (SuspensionManagerException)
                    {
                        // Fehler beim Wiederherstellen des Zustands.
                        // Annehmen, dass kein Zustand vorhanden ist und fortfahren
                    }
                }

                // Den Rahmen im aktuellen Fenster platzieren
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
#if WINDOWS_PHONE_APP
                // Entfernt die Drehkreuznavigation für den Start.
                if (rootFrame.ContentTransitions != null)
                {
                    transitions = new TransitionCollection();
                    foreach (Transition c in rootFrame.ContentTransitions)
                    {
                        transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += RootFrame_FirstNavigated;
#endif

                // Wenn der Navigationsstapel nicht wiederhergestellt wird, zur ersten Seite navigieren
                // und die neue Seite konfigurieren, indem die erforderlichen Informationen als Navigationsparameter
                // übergeben werden
                if (!rootFrame.Navigate(typeof (EnterURL), e.Arguments))
                {
                    throw new Exception("Failed to create initial page RIGHT HERE");
                }
            }

            // Sicherstellen, dass das aktuelle Fenster aktiv ist
            Window.Current.Activate();
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        ///     Stellt die Inhaltsübergänge nach dem Start der App wieder her.
        /// </summary>
        /// <param name="sender">Das Objekt, an das der Handler angefügt wird.</param>
        /// <param name="e">Details zum Navigationsereignis.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = transitions ?? new TransitionCollection {new NavigationThemeTransition()};
            rootFrame.Navigated -= RootFrame_FirstNavigated;
        }
#endif

        /// <summary>
        ///     Wird aufgerufen, wenn die Ausführung der Anwendung angehalten wird.  Der Anwendungszustand wird gespeichert,
        ///     ohne zu wissen, ob die Anwendung beendet oder fortgesetzt wird und die Speicherinhalte dabei
        ///     unbeschädigt bleiben.
        /// </summary>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            await SuspensionManager.SaveAsync();
            deferral.Complete();
        }
    }
}