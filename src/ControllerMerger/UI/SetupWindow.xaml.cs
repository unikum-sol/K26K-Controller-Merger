using System.ComponentModel;
using System.Windows;
using ControllerMerger.UI.ViewModels;

namespace ControllerMerger.UI;

/// <summary>Einrichtungsassistent für ViGEmBus. DialogResult = true, sobald der Treiber verfügbar ist.</summary>
public partial class SetupWindow : Window
{
    private readonly SetupViewModel? _vm;

    public SetupWindow()
    {
        InitializeComponent();
        AppMark.Source = IconFactory.CreateAppImage(32);
        TrayPreview.Source = IconFactory.CreateTrayImage(TrayState.Active, light: !ThemeManager.IsDark, size: 40);
        Frame.BorderThickness = new Thickness(ThemeManager.IsWindows11 ? 0 : 1);
        SourceInitialized += (_, _) => ThemeManager.ApplyFrame(this);
        DataContextChanged += (_, _) => UpdateHero();
    }

    internal SetupWindow(SetupViewModel vm) : this()
    {
        _vm = vm;
        DataContext = vm;
        vm.PropertyChanged += OnVmChanged;
        vm.Completed += () => { DialogResult = true; };
        vm.Dismissed += () => { DialogResult = false; };
        UpdateHero();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupViewModel.HeroKind)) UpdateHero();
    }

    /// <summary>Hero-Kachel einfärben (Akzent / Erfolg / Warnung / Fehler).</summary>
    public void UpdateHero()
    {
        if (DataContext is not SetupViewModel vm) return;
        (string bg, string fg) = vm.HeroKind switch
        {
            BadgeKind.Success => ("Brush.SuccessSubtle", "Brush.Success"),
            BadgeKind.Warning => ("Brush.WarningSubtle", "Brush.Warning"),
            BadgeKind.Danger => ("Brush.DangerSubtle", "Brush.Danger"),
            _ => ("Brush.AccentSubtle", "Brush.AccentText"),
        };
        Hero.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, bg);
        HeroGlyph.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, fg);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        // Während der Installation nicht einfach schließen – erst sauber abbrechen.
        if (_vm?.Stage == SetupStage.Working)
        {
            e.Cancel = true;
            _vm.SecondaryCommand.Execute(null);
            return;
        }
        base.OnClosing(e);
    }
}
