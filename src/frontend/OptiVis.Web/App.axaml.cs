using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.ViewModels;
using OptiVis.UI.Shared.Views;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;

namespace OptiVis.Web;

public partial class App : Application
{
    private ApiService? _apiService;
    private ISignalRService? _signalRService;
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();
        AvaloniaXamlLoader.Load(this);
        
        ThemeManager.Instance.Initialize();
        LanguageManager.Instance.Initialize();
        SettingsService.Instance.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var backendUrl = SettingsService.Instance.BackendUrl;
            var signalRHubUrl = SettingsService.Instance.SignalRHubUrl;

            _apiService = new ApiService(backendUrl);
            _signalRService = new SignalRService(signalRHubUrl);
            _mainViewModel = new MainViewModel(_apiService, _signalRService);

            singleViewPlatform.MainView = new ShellView
            {
                DataContext = _mainViewModel
            };

            SettingsService.Instance.OnBackendUrlChanged += OnBackendUrlChanged;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnBackendUrlChanged(string newUrl)
    {
        if (_apiService == null || _mainViewModel == null) return;

        _apiService.UpdateBaseUrl(newUrl);

        var oldSignalR = _signalRService;
        var newHubUrl = SettingsService.Instance.SignalRHubUrl;
        var newSignalR = new SignalRService(newHubUrl);

        try
        {
            await newSignalR.ConnectAsync();
        }
        catch
        {
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _mainViewModel.SetSignalRService(newSignalR);
        });

        _signalRService = newSignalR;

        if (oldSignalR != null)
        {
            try
            {
                await oldSignalR.DisposeAsync();
            }
            catch
            {
            }
        }
    }
}
