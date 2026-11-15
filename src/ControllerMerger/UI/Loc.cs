using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using ControllerMerger.Core;

namespace ControllerMerger.UI;

/// <summary>
/// Minimal localization layer with live switching (German / English).
/// XAML: <c>Text="{ui:T NavOverview}"</c> binds to <see cref="Loc.Instance"/>[key]; when the language changes,
/// <see cref="Loc"/> raises <c>Item[]</c> and every bound text updates immediately – no restart needed.
/// Code: <c>Loc.T("Key")</c> or <c>Loc.F("Key", args)</c>.
/// Reads are thread-safe (immutable tables, volatile language field), so the input thread may use them too.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private static volatile bool _english;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed (UI thread).</summary>
    public static event Action? Changed;

    public static UiLanguage Current => _english ? UiLanguage.English : UiLanguage.German;

    /// <summary>Windows display language: German if it is German, English otherwise.</summary>
    public static UiLanguage SystemDefault =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? UiLanguage.German : UiLanguage.English;

    public static void SetLanguage(UiLanguage language)
    {
        bool english = language == UiLanguage.English;
        if (english == _english) return;
        _english = english;
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
        Changed?.Invoke();
    }

    public string this[string key] => T(key);

    public static string T(string key) =>
        Strings.Table.TryGetValue(key, out var pair) ? (_english ? pair.En : pair.De) : $"[{key}]";

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
}

/// <summary>XAML markup extension: <c>{ui:T Key}</c> → live-updating binding to the localized string.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class T : MarkupExtension
{
    public T() { }

    public T(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
