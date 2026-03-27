using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.ViewModels;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using System;

namespace OptiVis.Desktop;

public partial class App : Application
{
    private ApiService? _apiService;
    private SignalRService? _signalRService;
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();
        AvaloniaXamlLoader.Load(this);
        
        ThemeManager.Instance.Initialize();
        LanguageManager.Instance.Initialize();
        SettingsService.Instance.Initialize();
    }

	public override async void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var startupWarning = SettingsService.Instance.ConsumeStartupWarning();
			var backendUrl = SettingsService.Instance.BackendUrl;

			if (!TryInitializeDesktop(desktop, backendUrl, out var startupError))
			{
				var fallbackApplied = SettingsService.Instance.TryUpdateBackendUrl(
					SettingsService.DefaultBackendUrl,
					out var fallbackUrl,
					out _);

				if (!fallbackApplied || !TryInitializeDesktop(desktop, fallbackUrl, out startupError))
				{
					Program.ShowStartupMessage(
						$"Dastur ishga tushmadi:\n{startupError?.Message ?? "Noma'lum xatolik"}",
						isError: true);
					return;
				}

				Program.ShowStartupMessage(
					$"Noto'g'ri Backend URL aniqlangani uchun standart manzil qo'llandi:\n{fallbackUrl}",
					isError: false);
			}
			else if (!string.IsNullOrWhiteSpace(startupWarning))
			{
				Program.ShowStartupMessage(startupWarning, isError: false);
			}

			if (_signalRService != null)
			{
				try
				{
					await _signalRService.ConnectAsync();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"SignalR ulanishda xato: {ex.Message}");
				}
			}

			SettingsService.Instance.OnBackendUrlChanged += OnBackendUrlChanged;
		}

		base.OnFrameworkInitializationCompleted();
	}

	private bool TryInitializeDesktop(
		IClassicDesktopStyleApplicationLifetime desktop,
		string backendUrl,
		out Exception? error)
	{
		try
		{
			_apiService = new ApiService(backendUrl);
			_signalRService = new SignalRService(SettingsService.BuildSignalRHubUrl(backendUrl));
			_mainViewModel = new MainViewModel(_apiService, _signalRService);

			desktop.MainWindow = new MainWindow
			{
				DataContext = _mainViewModel
			};

			error = null;
			return true;
		}
		catch (Exception ex)
		{
			error = ex;
			return false;
		}
	}

	private async void OnBackendUrlChanged(string newUrl)
    {
        if (_apiService == null || _signalRService == null) return;

		try
		{
			_apiService.UpdateBaseUrl(newUrl);

			await _signalRService.DisconnectAsync();
			await _signalRService.DisposeAsync();

			_signalRService = new SignalRService(SettingsService.BuildSignalRHubUrl(newUrl));
			_mainViewModel?.SetSignalRService(_signalRService);

			await _signalRService.ConnectAsync();
		}
		catch (Exception ex)
		{
			Program.ShowStartupMessage(
				$"Backend URL ni qo'llashda xatolik:\n{ex.Message}",
				isError: true);
		}
    }
}
