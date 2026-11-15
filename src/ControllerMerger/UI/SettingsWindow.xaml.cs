using System.Windows;

namespace ControllerMerger.UI;

/// <summary>Feste Texte, die als Liste im XAML verwendet werden.</summary>
public static class SettingsTexts
{
    public const string RuleButtons = "Eine Taste gilt als gedrückt, sobald sie an irgendeinem Controller gedrückt ist.";
    public const string RuleTriggers = "Bei den Triggern zählt der stärkere Druck.";
    public const string RuleDpad = "Drücken zwei Controller gegensätzliche Richtungen am Steuerkreuz, heben sie sich auf.";
}

/// <summary>Einstellungsfenster (Doppelklick auf das Tray-Symbol). Wird bei Bedarf erzeugt und beim Schließen freigegeben.</summary>
public partial class SettingsWindow : Window
{
    public enum Page { Overview, Input, General }

    /// <summary>Maximale Inhaltsbreite – für alle Seiten identisch (Design-Council R1, Raster).</summary>
    private const double MaxContentWidth = 600;

    public SettingsWindow()
    {
        InitializeComponent();
        AppMark.Source = IconFactory.CreateAppImage(36);
        Frame.BorderThickness = new Thickness(ThemeManager.IsWindows11 ? 0 : 1);
        SourceInitialized += (_, _) => ThemeManager.ApplyFrame(this);
        StateChanged += (_, _) =>
            Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        Scroller.SizeChanged += (_, _) => UpdateContentWidth(Scroller.ActualWidth);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is System.ComponentModel.INotifyPropertyChanged o) o.PropertyChanged -= OnVmChanged;
            if (e.NewValue is System.ComponentModel.INotifyPropertyChanged n) n.PropertyChanged += OnVmChanged;
            UpdateModeHighlight();
        };
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.StickMode)) UpdateModeHighlight();
    }

    /// <summary>Hebt die Erklärung der gewählten Stick-Option hervor, die andere tritt zurück.</summary>
    private void UpdateModeHighlight()
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        bool perStick = vm.StickMode == Core.StickMergeMode.PerStick;
        Emphasize(ModeStickName, ModeStickText, perStick);
        Emphasize(ModeAxisName, ModeAxisText, !perStick);
    }

    private static void Emphasize(System.Windows.Controls.TextBlock name, System.Windows.Controls.TextBlock text, bool selected)
    {
        name.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        name.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, selected ? "Brush.Text" : "Brush.TextTertiary");
        text.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, selected ? "Brush.TextSecondary" : "Brush.TextTertiary");
    }

    /// <summary>Inhalt immer gleich breit: min(600, verfügbarer Platz).</summary>
    public void UpdateContentWidth(double available)
    {
        var m = ContentHost.Margin;
        ContentHost.Width = Math.Max(320, Math.Min(MaxContentWidth, available - m.Left - m.Right - 8));
    }

    public void ShowPage(Page page)
    {
        (page switch
        {
            Page.Input => NavInput,
            Page.General => NavGeneral,
            _ => NavOverview,
        }).IsChecked = true;
        UpdatePages();
    }

    private void OnNavChecked(object sender, RoutedEventArgs e) => UpdatePages();

    private void UpdatePages()
    {
        if (PageOverview is null) return; // während InitializeComponent
        PageOverview.Visibility = NavOverview.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageInput.Visibility = NavInput.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageGeneral.Visibility = NavGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Scroller.ScrollToTop();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
