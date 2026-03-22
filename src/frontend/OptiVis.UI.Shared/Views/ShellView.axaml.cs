using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using OptiVis.UI.Shared.ViewModels;

namespace OptiVis.UI.Shared.Views;

public partial class ShellView : UserControl
{
    private double _sidebarWidth = 240;
    private MainViewModel? _vm;
    private CancellationTokenSource? _animCts;

    public ShellView()
    {
        InitializeComponent();
        SidebarBorder.Width = 240;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as MainViewModel;
        if (_vm == null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _sidebarWidth = _vm.IsSidebarCollapsed ? 60 : 240;
        SidebarBorder.Width = _sidebarWidth;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarCollapsed))
            Dispatcher.UIThread.Post(() => AnimateSidebar(_vm?.IsSidebarCollapsed ?? false));
    }

    private async void AnimateSidebar(bool collapsed)
    {
        _animCts?.Cancel();
        _animCts = new CancellationTokenSource();

        var target = collapsed ? 60.0 : 240.0;
        var from = _sidebarWidth;
        _sidebarWidth = target;

        var anim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseInOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Border.WidthProperty, from) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Border.WidthProperty, target) } }
            }
        };

        try { await anim.RunAsync(SidebarBorder, _animCts.Token); }
        catch (OperationCanceledException) { }
    }

    private void OnNavDashboard(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigateTo("Dashboard");
    }

    private void OnNavOperators(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigateTo("Operators");
    }

    private void OnNavSearch(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigateTo("Search");
    }

    private void OnNavSettings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigateTo("Settings");
    }
}
