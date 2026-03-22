using ReactiveUI;
using OptiVis.UI.Shared.i18n;
using OptiVis.UI.Shared.Extensions;
using Avalonia.Threading;

namespace OptiVis.UI.Shared.ViewModels;

public abstract class LocalizedViewModelBase : ReactiveObject
{
    protected LocalizedViewModelBase()
    {
        Translations.OnLanguageChanged += OnTranslationsLanguageChanged;
        LocalizationSource.Instance.PropertyChanged += (s, e) => OnLocalizationSourceChanged();
    }

    private void OnTranslationsLanguageChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            OnLanguageChanged();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(OnLanguageChanged);
        }
    }

    private void OnLocalizationSourceChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            OnLanguageChanged();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(OnLanguageChanged);
        }
    }

    protected virtual void OnLanguageChanged()
    {
        this.RaisePropertyChanged(nameof(NavTitle));
        this.RaisePropertyChanged(string.Empty);
    }

    public abstract string NavTitle { get; }
}
