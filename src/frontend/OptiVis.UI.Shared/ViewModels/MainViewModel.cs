using ReactiveUI;
using System.Windows.Input;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.i18n;
using Avalonia.Threading;

namespace OptiVis.UI.Shared.ViewModels;

public class MainViewModel : ReactiveObject, IDisposable
{
    private readonly IApiService _apiService;
    private ISignalRService _signalRService;
    private readonly IOperatorProfileService _operatorProfileService;

    private object? _currentView;
    public object? CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    private string _currentPage = "Dashboard";
    public string CurrentPage
    {
        get => _currentPage;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentPage, value);
            this.RaisePropertyChanged(nameof(IsDashboardActive));
            this.RaisePropertyChanged(nameof(IsOperatorsActive));
            this.RaisePropertyChanged(nameof(IsSearchActive));
            this.RaisePropertyChanged(nameof(IsSettingsActive));
        }
    }

    public bool IsDashboardActive  => CurrentPage == "Dashboard";
    public bool IsOperatorsActive  => CurrentPage == "Operators" || CurrentPage == "OperatorDetail";
    public bool IsSearchActive     => CurrentPage == "Search";
    public bool IsSettingsActive   => CurrentPage == "Settings";

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    private bool _isSidebarCollapsed;
    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set => this.RaiseAndSetIfChanged(ref _isSidebarCollapsed, value);
    }

    public ICommand ToggleSidebarCommand { get; }

    public DashboardViewModel DashboardVM { get; }
    public OperatorsViewModel OperatorsVM { get; }
    public SearchViewModel SearchVM       { get; }
    public SettingsViewModel SettingsVM   { get; }

    public MainViewModel(IApiService apiService, ISignalRService signalRService)
    {
        _apiService = apiService;
        _signalRService = signalRService;
        _operatorProfileService = new LocalOperatorProfileService();

        ToggleSidebarCommand = ReactiveCommand.Create(() => IsSidebarCollapsed = !IsSidebarCollapsed);

        DashboardVM = new DashboardViewModel(apiService, signalRService);
        OperatorsVM = new OperatorsViewModel(apiService, signalRService, _operatorProfileService, NavigateToOperatorDetail);
        SearchVM    = new SearchViewModel(apiService, _operatorProfileService);
        SettingsVM  = new SettingsViewModel(_operatorProfileService, signalRService);

        CurrentView = DashboardVM;

        SubscribeToSignalR();
        Translations.OnLanguageChanged += OnLanguageChanged;

        _ = InitializeAsync();
    }

    private void SubscribeToSignalR()
    {
        _signalRService.OnConnectionChanged += connected =>
        {
            Dispatcher.UIThread.Post(() => IsConnected = connected);
        };
        IsConnected = _signalRService.IsConnected;
    }

    public void SetSignalRService(ISignalRService newService)
    {
        _signalRService = newService;
        SubscribeToSignalR();
        DashboardVM.SetSignalRService(newService);
        OperatorsVM.SetSignalRService(newService);
        SettingsVM.SetSignalRService(newService);
    }

    private async Task InitializeAsync()
    {
        await DashboardVM.LoadDataAsync();
        await OperatorsVM.LoadDataAsync();
        await SearchVM.LoadDataAsync();
    }

    public void NavigateTo(string page)
    {
        CurrentPage = page;
        CurrentView = page switch
        {
            "Dashboard" => DashboardVM,
            "Operators" => OperatorsVM,
            "Search"    => SearchVM,
            "Settings"  => SettingsVM,
            _           => DashboardVM
        };

        switch (page)
        {
            case "Operators":
                _ = OperatorsVM.LoadDataAsync();
                break;
            case "Search":
                _ = SearchVM.LoadDataAsync();
                break;
            case "Settings":
                _ = SettingsVM.LoadOperatorProfilesAsync();
                break;
        }
    }

    public void NavigateToOperatorDetail(OperatorStats operatorStats)
    {
        CurrentPage = "OperatorDetail";
        CurrentView = new OperatorDetailViewModel(_apiService, operatorStats, () => NavigateTo("Operators"));
    }

    private void OnLanguageChanged()
    {
        this.RaisePropertyChanged(nameof(DashboardVM));
        this.RaisePropertyChanged(nameof(OperatorsVM));
        this.RaisePropertyChanged(nameof(SearchVM));
        this.RaisePropertyChanged(nameof(SettingsVM));
    }

    public void Dispose()
    {
        Translations.OnLanguageChanged -= OnLanguageChanged;
    }
}
