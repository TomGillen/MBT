﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MediaBrowser.ApiInteraction;
using MediaBrowser.ApiInteraction.WebSocket;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Constants;
using MediaBrowser.Common.Implementations;
using MediaBrowser.Common.Implementations.ScheduledTasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Updates;
using MediaBrowser.Theater.Api;
using MediaBrowser.Theater.Api.Commands;
using MediaBrowser.Theater.Api.Configuration;
using MediaBrowser.Theater.Api.Events;
using MediaBrowser.Theater.Api.Navigation;
using MediaBrowser.Theater.Api.Playback;
using MediaBrowser.Theater.Api.Session;
using MediaBrowser.Theater.Api.System;
using MediaBrowser.Theater.Api.UserInput;
using MediaBrowser.Theater.Api.UserInterface;
using MediaBrowser.Theater.DefaultTheme;
using MediaBrowser.Theater.DirectShow;
using MediaBrowser.Theater.Networking;
using MediaBrowser.Theater.Presentation.Playback;
using MediaBrowser.Theater.StartupWizard;
using MediaBrowser.Theater.StartupWizard.ViewModels;
using Application = System.Windows.Application;

namespace MediaBrowser.Theater
{
    /// <summary>
    ///     Class CompositionRoot
    /// </summary>
    internal class ApplicationHost : BaseApplicationHost<ApplicationPaths>, IDisposable, ITheaterApplicationHost
    {
        public ApplicationHost(ApplicationPaths applicationPaths, ILogManager logManager)
            : base(applicationPaths, logManager) { }

        public IApiClient ApiClient { get; private set; }
        internal ApiWebSocket ApiWebSocket { get; set; }
        public ITheme Theme { get; private set; }
        public bool RestartOnExit { get; private set; }

        public ITheaterConfigurationManager TheaterConfigurationManager
        {
            get { return (ConfigurationManager) ConfigurationManager; }
        }

        public INavigator Navigator { get; private set; }
        public IPresenter Presenter { get; private set; }
        public IUserInputManager UserInputManager { get; private set; }

        public override bool CanSelfRestart
        {
            get { return true; }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this instance can self update.
        /// </summary>
        /// <value><c>true</c> if this instance can self update; otherwise, <c>false</c>.</value>
        public override bool CanSelfUpdate
        {
            get
            {
#if DEBUG
                return false;
#endif
                return true;
            }
        }

        public override string Name
        {
            get { return "Media Browser Theater"; }
        }

        public override bool IsRunningAsService
        {
            get { return false; }
        }

        public override async Task Init(IProgress<double> progress)
        {
            await base.Init(progress).ConfigureAwait(false);
            await RunStartupTasks().ConfigureAwait(false);

            Action<Window> mainWindowLoaded = null;
            mainWindowLoaded = w => {
                StartEntryPoints();
                Presenter.MainWindowLoaded -= mainWindowLoaded;
            };

            Presenter.MainWindowLoaded += mainWindowLoaded;

            Logger.Info("Core startup complete");
        }

        /// <summary>
        ///     Registers resources that classes will depend on
        /// </summary>
        protected override async Task RegisterResources(IProgress<double> progress)
        {
            ReloadApiClient();

            await base.RegisterResources(progress).ConfigureAwait(false);

            RegisterSingleInstance(ApplicationPaths);
            RegisterSingleInstance(ApiClient);
            RegisterSingleInstance<IServerEvents>(ApiWebSocket);
            RegisterSingleInstance(TheaterConfigurationManager);
            RegisterSingleInstance<ITheaterApplicationHost>(this);

            Container.RegisterSingle(typeof (ITheme), FindTheme());
            Container.RegisterSingle(typeof (IMediaFilters), typeof (MediaFilters));
            Container.RegisterSingle(typeof (IEventAggregator), typeof (EventAggregator));
            Container.RegisterSingle(typeof (ISessionManager), typeof (SessionManager));
            Container.RegisterSingle(typeof (IImageManager), typeof (ImageManager));
            Container.RegisterSingle(typeof (IInternalPlayerWindowManager), typeof (InternalPlayerWindowManager));
            Container.RegisterSingle(typeof (IPlaybackManager), typeof (PlaybackManager));
            Container.RegisterSingle(typeof (IServerConnectionManager), typeof (ServerConnectionManager));
            Container.RegisterSingle(typeof (IUserInputManager), typeof (UserInputManager));
            Container.RegisterSingle(typeof (ICommandManager), typeof (CommandManager));

            // temp bindings until it is possible for the theme to bind these
            Container.RegisterSingle(typeof (IPresenter), typeof (Presenter));
            Container.RegisterSingle(typeof (INavigator), typeof (Navigator));
            Container.RegisterSingle(typeof (WindowManager), typeof (WindowManager));
        }

        protected override void FindParts()
        {
            Theme = Resolve<ITheme>();
            Plugins = LoadPlugins();
            Resolve<IPlaybackManager>().AddParts(GetExports<IMediaPlayer>());

            Navigator = Resolve<INavigator>();
            Presenter = Resolve<IPresenter>();
            UserInputManager = Resolve<IUserInputManager>();
        }

        public void StartEntryPoints()
        {
            Parallel.ForEach(GetExports<IStartupEntryPoint>(), entryPoint =>
            {
                try
                {
                    entryPoint.Run();
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in {0}", ex, entryPoint.GetType().Name);
                }
            });
        }

        private Type FindTheme()
        {
            IEnumerable<Type> themeTypes = GetExportTypes<ITheme>();

            Type activeThemeType = themeTypes.FirstOrDefault(t => GetGuid(t) == TheaterConfigurationManager.Configuration.ActiveThemeGuid);

            if (activeThemeType == null) {
                throw new InvalidOperationException("No theme loaded");
            }

            return activeThemeType;
        }

        private IEnumerable<IPlugin> LoadPlugins()
        {
            List<IPlugin> nonThemePlugins = GetExportTypes<IPlugin>().Where(t => !typeof (ITheme).IsAssignableFrom(t))
                                                                     .Select(CreateInstanceSafe)
                                                                     .Where(i => i != null)
                                                                     .Cast<IPlugin>()
                                                                     .ToList();

            return new List<IPlugin>(nonThemePlugins) { Theme };
        }

        private Guid GetGuid(Type type)
        {
            var attribute = (GuidAttribute) type.Assembly.GetCustomAttributes(typeof (GuidAttribute), true)[0];
            return new Guid(attribute.Value);
        }

        public void RunUserInterface()
        {
            Theme.Run();
        }

        protected override INetworkManager CreateNetworkManager()
        {
            return new NetworkManager();
        }

        /// <summary>
        ///     Disposes the current ApiClient and creates a new one
        /// </summary>
        private void ReloadApiClient()
        {
            ILogger logger = LogManager.GetLogger("ApiClient");

            var apiClient = new ApiClient(new HttpWebRequestClient(logger), logger, TheaterConfigurationManager.Configuration.ServerHostName, TheaterConfigurationManager.Configuration.ServerApiPort, "Media Browser Theater", Environment.MachineName, Environment.MachineName, ApplicationVersion.ToString()) {
                JsonSerializer = JsonSerializer,
                ImageQuality = TheaterConfigurationManager.Configuration.DownloadCompressedImages
                                   ? 90
                                   : 100
            };

            ApiClient = apiClient;

            logger = LogManager.GetLogger("ApiWebSocket");

            // WebSocketEntry point will handle figuring out the port and connecting
            ApiWebSocket = new ApiWebSocket(logger, JsonSerializer, apiClient.ServerHostName, 0,
                                            ApiClient.DeviceId, apiClient.ApplicationVersion,
                                            apiClient.ClientName, apiClient.DeviceName, () => ClientWebSocketFactory.CreateWebSocket(logger));

            apiClient.WebSocketConnection = ApiWebSocket;
        }

        public override async Task Restart()
        {
            await Shutdown();
            RestartOnExit = true;
        }

        /// <summary>
        ///     Gets the composable part assemblies.
        /// </summary>
        /// <returns>IEnumerable{Assembly}.</returns>
        protected override IEnumerable<Assembly> GetComposablePartAssemblies()
        {
            // Gets all plugin assemblies by first reading all bytes of the .dll and calling Assembly.Load against that
            // This will prevent the .dll file from getting locked, and allow us to replace it when needed
            foreach (Assembly pluginAssembly in GetPluginAssemblies()) {
                yield return pluginAssembly;
            }

            // Include composable parts in the Model assembly 
            yield return typeof (SystemInfo).Assembly;

            // Include composable parts in the Common assembly 
            yield return typeof (IApplicationPaths).Assembly;

            // Common implementations
            yield return typeof (TaskManager).Assembly;

            // DirectShow assembly
            yield return typeof (InternalDirectShowPlayer).Assembly;
            
            // Presentation assembly
            yield return typeof (GenericExternalPlayer).Assembly;

            // Default theme
            yield return typeof (Theme).Assembly;

            // Include composable parts in the running assembly
            yield return GetType().Assembly;
        }

        /// <summary>
        ///     Gets the plugin assemblies.
        /// </summary>
        /// <returns>IEnumerable{Assembly}.</returns>
        private IEnumerable<Assembly> GetPluginAssemblies()
        {
            //todo only load the active external theme assembly

            try {
                return Directory.EnumerateFiles(ApplicationPaths.PluginsPath, "*.dll", SearchOption.TopDirectoryOnly)
                                .Select(LoadAssembly)
                                .Where(a => a != null)
                                .ToList();
            }
            catch (DirectoryNotFoundException) {
                return new List<Assembly>();
            }
        }

        public override async Task Shutdown()
        {
            await Theme.Shutdown().ConfigureAwait(false);

            if (Application.Current != null) {
                Application.Current.Shutdown();
            }
        }

        protected override void OnConfigurationUpdated(object sender, EventArgs e)
        {
            base.OnConfigurationUpdated(sender, e);

            ((ApiClient) ApiClient).ImageQuality = TheaterConfigurationManager.Configuration.DownloadCompressedImages
                                                       ? 90
                                                       : 100;
        }

        protected override IConfigurationManager GetConfigurationManager()
        {
            return new ConfigurationManager(ApplicationPaths, LogManager, XmlSerializer);
        }

        /// <summary>
        ///     Checks for update.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task{CheckForUpdateResult}.</returns>
        public override async Task<CheckForUpdateResult> CheckForApplicationUpdate(CancellationToken cancellationToken,
                                                                                   IProgress<double> progress)
        {
            SystemInfo serverInfo = await ApiClient.GetSystemInfoAsync().ConfigureAwait(false);

            IEnumerable<PackageInfo> availablePackages = await InstallationManager.GetAvailablePackagesWithoutRegistrationInfo(cancellationToken).ConfigureAwait(false);

            var serverVersion = new Version(serverInfo.Version);

            PackageVersionInfo version = InstallationManager.GetLatestCompatibleVersion(availablePackages, Constants.MbTheaterPkgName, null, serverVersion, ConfigurationManager.CommonConfiguration.SystemUpdateLevel);

            return version != null ? new CheckForUpdateResult { AvailableVersion = version.version, IsUpdateAvailable = version.version > ApplicationVersion, Package = version } :
                       new CheckForUpdateResult { AvailableVersion = ApplicationVersion, IsUpdateAvailable = false };
        }

        /// <summary>
        ///     Updates the application.
        /// </summary>
        /// <param name="package">The package that contains the update</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task.</returns>
        public override async Task UpdateApplication(PackageVersionInfo package, CancellationToken cancellationToken, IProgress<double> progress)
        {
            await InstallationManager.InstallPackage(package, progress, cancellationToken).ConfigureAwait(false);

            OnApplicationUpdated(package.version);
        }

        protected override void ConfigureAutoRunAtStartup(bool autorun)
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Media Browser 3", "Media Browser Theater.lnk");

            if (autorun) {
                // Copy our shortut into the startup folder for this user
                File.Copy(shortcutPath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Path.GetFileName(shortcutPath) ?? "MediaBrowserTheaterStartup.lnk"), true);
            } else {
                // Remove our shortcut from the startup folder for this user
                File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Path.GetFileName(shortcutPath) ?? "MediaBrowserTheaterStartup.lnk"));
            }
        }

        public void ShutdownSystem()
        {
            Process.Start("shutdown", "/s /t 0");
        }

        public void RebootSystem()
        {
            Process.Start("shutdown", "/r /t 0");
        }

        public void SetSystemToSleep()
        {
            System.Windows.Forms.Application.SetSuspendState(PowerState.Suspend, false, false);
        }

        public bool RunStartupWizard()
        {
            var app = new StartupWizardApp();

            var wizard = new WizardViewModel(new List<IWizardPage> {
                Resolve<IntroductionViewModel>(),
                Resolve<ServerDetailsViewModel>(),
                Resolve<PrerequisitesViewModel>(),
            });

            bool completed = false;
            wizard.Completed += status => {
                completed = status == WizardCompletionStatus.Finished;
                app.Shutdown();
            };

            var window = new StartupWizardWindow {
                DataContext = wizard
            };

            app.Run(window);
            return completed;
        }
    }
}