using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using OptiVis.UI.Shared.i18n;
using OptiVis.UI.Shared.Extensions;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.Services;

namespace OptiVis.UI.Shared.ViewModels;

public class SettingsViewModel : LocalizedViewModelBase
{
	private readonly IOperatorProfileService _profileService;
	private ISignalRService? _signalRService;

	public override string NavTitle => Translations.Get("Settings");

	// ─── Tema ────────────────────────────────────────────────────────────────
	private string _selectedTheme = "Dark";
	public string SelectedTheme
	{
		get => _selectedTheme;
		set
		{
			this.RaiseAndSetIfChanged(ref _selectedTheme, value);
			OnThemeChanged(value);
		}
	}

	// ─── Til ─────────────────────────────────────────────────────────────────
	private string _selectedLanguage = "O'zbekcha (Lotin)";
	public string SelectedLanguage
	{
		get => _selectedLanguage;
		set
		{
			this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
			OnLanguageChangedInternal(value);
		}
	}

	public string[] AvailableThemes => ["Dark", "Light"];
	public string[] AvailableLanguages => LanguageManager.AvailableLanguageNames;

	// ─── Backend URL ─────────────────────────────────────────────────────────
	private string _backendUrl = "https://tel-mon.hamrohmmt.uz/";
	public string BackendUrl
	{
		get => _backendUrl;
		set => this.RaiseAndSetIfChanged(ref _backendUrl, value);
	}

	private string _backendUrlMessage = string.Empty;
	public string BackendUrlMessage
	{
		get => _backendUrlMessage;
		set
		{
			this.RaiseAndSetIfChanged(ref _backendUrlMessage, value);
			this.RaisePropertyChanged(nameof(HasBackendUrlMessage));
		}
	}

	public bool HasBackendUrlMessage => !string.IsNullOrWhiteSpace(BackendUrlMessage);

	private string _backendUrlMessageColor = "#10B981";
	public string BackendUrlMessageColor
	{
		get => _backendUrlMessageColor;
		set => this.RaiseAndSetIfChanged(ref _backendUrlMessageColor, value);
	}

	public ReactiveCommand<Unit, Unit> SaveBackendUrlCommand { get; }
	public ReactiveCommand<Unit, Unit> ReconnectCommand { get; }

	private bool _isConnected;
	public bool IsConnected
	{
		get => _isConnected;
		set
		{
			this.RaiseAndSetIfChanged(ref _isConnected, value);
			this.RaisePropertyChanged(nameof(ConnectionStatusText));
			this.RaisePropertyChanged(nameof(ConnectionStatusColor));
		}
	}

	private bool _isConnecting;
	public bool IsConnecting
	{
		get => _isConnecting;
		set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
	}

	public string ConnectionStatusText => IsConnected 
		? Translations.Get("Connected") 
		: Translations.Get("Disconnected");

	public string ConnectionStatusColor => IsConnected ? "#10B981" : "#EF4444";

	// ─── Operator boshqaruvi ─────────────────────────────────────────────────
	public ObservableCollection<OperatorProfileItem> OperatorProfiles { get; } = new();

	private bool _isLoadingProfiles;
	public bool IsLoadingProfiles
	{
		get => _isLoadingProfiles;
		set => this.RaiseAndSetIfChanged(ref _isLoadingProfiles, value);
	}

	// ─── Operator o'chirish confirmation ─────────────────────────────────────
	private bool _showRemoveConfirmation;
	public bool ShowRemoveConfirmation
	{
		get => _showRemoveConfirmation;
		set => this.RaiseAndSetIfChanged(ref _showRemoveConfirmation, value);
	}

	private string _operatorToRemove = string.Empty;
	public string OperatorToRemove
	{
		get => _operatorToRemove;
		set => this.RaiseAndSetIfChanged(ref _operatorToRemove, value);
	}

	public ReactiveCommand<string, Unit> RemoveOperatorCommand { get; }
	public ReactiveCommand<Unit, Unit> ConfirmRemoveCommand { get; }
	public ReactiveCommand<Unit, Unit> CancelRemoveCommand { get; }

	// ─── Versiya va Copyright ────────────────────────────────────────────────
	public string AppVersion => "0.0.1";
	public string Copyright => "© 2026 Ovoza dasturlar";
	public string TelegramLink => "t.me/ovozadasturlar";

	public SettingsViewModel() : this(new LocalOperatorProfileService(), null) { }

	public SettingsViewModel(IOperatorProfileService profileService, ISignalRService? signalRService)
	{
		_profileService = profileService;
		_signalRService = signalRService;

		var currentLang = LanguageManager.Instance.CurrentLanguage;
		_selectedLanguage = LanguageManager.ToDisplayName(currentLang);

		var currentTheme = ThemeManager.Instance.CurrentTheme;
		_selectedTheme = currentTheme == AppTheme.Light ? "Light" : "Dark";

		_backendUrl = SettingsService.Instance.BackendUrl;
		_isConnected = signalRService?.IsConnected ?? false;

		if (_signalRService != null)
		{
			_signalRService.OnConnectionChanged += OnConnectionChanged;
		}

		var canConfirmRemove = this
			.WhenAnyValue(x => x.ShowRemoveConfirmation)
			.ObserveOn(RxApp.MainThreadScheduler);

		var canReconnect = this
			.WhenAnyValue(x => x.IsConnecting)
			.Select(x => !x)
			.ObserveOn(RxApp.MainThreadScheduler);

		SaveBackendUrlCommand = ReactiveCommand.Create(
			SaveBackendUrl,
			outputScheduler: RxApp.MainThreadScheduler);

		ReconnectCommand = ReactiveCommand.CreateFromTask(
			ReconnectAsync,
			canReconnect,
			outputScheduler: RxApp.MainThreadScheduler);

		RemoveOperatorCommand = ReactiveCommand.Create<string>(
			ShowRemoveConfirmationDialog,
			outputScheduler: RxApp.MainThreadScheduler);

		ConfirmRemoveCommand = ReactiveCommand.CreateFromTask(
			ConfirmRemoveOperatorAsync,
			canConfirmRemove,
			outputScheduler: RxApp.MainThreadScheduler);

		CancelRemoveCommand = ReactiveCommand.Create(
			CancelRemove,
			outputScheduler: RxApp.MainThreadScheduler);

		_ = LoadOperatorProfilesAsync();
	}

	private void OnConnectionChanged(bool connected)
	{
		Dispatcher.UIThread.Post(() => IsConnected = connected);
	}

	public void SetSignalRService(ISignalRService newService)
	{
		if (_signalRService != null)
		{
			_signalRService.OnConnectionChanged -= OnConnectionChanged;
		}
		_signalRService = newService;
		_signalRService.OnConnectionChanged += OnConnectionChanged;
		IsConnected = newService.IsConnected;
	}

	private void SaveBackendUrl()
	{
		if (SettingsService.Instance.TryUpdateBackendUrl(_backendUrl, out var normalizedUrl, out var validationError))
		{
			BackendUrl = normalizedUrl;
			BackendUrlMessage = Translations.Get("BackendUrlSaved");
			BackendUrlMessageColor = "#10B981";
			return;
		}

		BackendUrlMessage = string.IsNullOrWhiteSpace(validationError)
			? Translations.Get("BackendUrlInvalid")
			: validationError;
		BackendUrlMessageColor = "#EF4444";
	}

	private async Task ReconnectAsync()
	{
		if (_signalRService == null) return;

		IsConnecting = true;
		try
		{
			await _signalRService.DisconnectAsync();
			await _signalRService.ConnectAsync();
		}
		finally
		{
			IsConnecting = false;
		}
	}

	private void ShowRemoveConfirmationDialog(string extension)
	{
		OperatorToRemove = extension;
		ShowRemoveConfirmation = true;
	}

	public void RequestRemoveOperator(string extension)
	{
		ShowRemoveConfirmationDialog(extension);
	}

	private async Task ConfirmRemoveOperatorAsync()
	{
		if (string.IsNullOrEmpty(OperatorToRemove)) return;

		await RemoveOperatorAsync(OperatorToRemove);

		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			ShowRemoveConfirmation = false;
			OperatorToRemove = string.Empty;
		});
	}

	private void CancelRemove()
	{
		ShowRemoveConfirmation = false;
		OperatorToRemove = string.Empty;
	}

	public async Task RemoveOperatorAsync(string extension)
	{
		if (_profileService is LocalOperatorProfileService localService)
			await localService.RemoveProfileAsync(extension);

		var item = OperatorProfiles.FirstOrDefault(p => p.Extension == extension);
		if (item != null)
			await Dispatcher.UIThread.InvokeAsync(() => OperatorProfiles.Remove(item));
	}

	public async Task LoadOperatorProfilesAsync()
	{
		await Dispatcher.UIThread.InvokeAsync(() => IsLoadingProfiles = true);
		try
		{
			var profiles = await _profileService.GetAllProfilesAsync();
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				OperatorProfiles.Clear();
				foreach (var kvp in profiles.OrderBy(p => p.Key))
				{
					OperatorProfiles.Add(new OperatorProfileItem
					{
						Extension = kvp.Key,
						Name = kvp.Value.Name,
						MobileNumber = kvp.Value.MobileNumber,
						AvatarColor = LocalOperatorProfileService.GetAvatarColor(kvp.Key)
					});
				}
			});
		}
		finally
		{
			await Dispatcher.UIThread.InvokeAsync(() => IsLoadingProfiles = false);
		}
	}

	public async Task SaveProfileAsync(string extension, string name, string? mobile)
	{
		await _profileService.SaveProfileAsync(new OperatorProfile
		{
			Extension = extension,
			Name = name.Trim(),
			MobileNumber = string.IsNullOrWhiteSpace(mobile) ? null : mobile.Trim()
		});

		var item = OperatorProfiles.FirstOrDefault(p => p.Extension == extension);
		if (item != null)
		{
			await Dispatcher.UIThread.InvokeAsync(() => item.IsSaved = true);
			await Task.Delay(2000);
			await Dispatcher.UIThread.InvokeAsync(() => item.IsSaved = false);
		}
	}

	public async Task AddManualOperatorAsync(string extension, string name)
	{
		if (string.IsNullOrWhiteSpace(extension)) return;

		extension = extension.Trim();
		if (OperatorProfiles.Any(p => p.Extension == extension)) return;

		await _profileService.SaveProfileAsync(new OperatorProfile
		{
			Extension = extension,
			Name = name.Trim()
		});

		await Dispatcher.UIThread.InvokeAsync(() => OperatorProfiles.Add(new OperatorProfileItem
		{
			Extension = extension,
			Name = name.Trim(),
			AvatarColor = LocalOperatorProfileService.GetAvatarColor(extension)
		}));
	}

	private void OnThemeChanged(string theme)
	{
		var appTheme = theme == "Light" ? AppTheme.Light : AppTheme.Dark;
		ThemeManager.Instance.CurrentTheme = appTheme;
	}

	private void OnLanguageChangedInternal(string language)
	{
		var appLang = LanguageManager.FromDisplayName(language);
		LanguageManager.Instance.CurrentLanguage = appLang;
		this.RaisePropertyChanged(nameof(ConnectionStatusText));
	}

	protected override void OnLanguageChanged()
	{
		base.OnLanguageChanged();
		this.RaisePropertyChanged(nameof(ConnectionStatusText));
	}
}
