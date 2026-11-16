using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows.Input;
using ControllerMerger.Services;
using ControllerMerger.Setup;

namespace ControllerMerger.UI.ViewModels;

public enum SetupStage { Welcome, Repair, Working, Success, RebootRequired, Failed }

public enum StepState { Pending, Active, Done, Failed }

/// <summary>Ein Schritt im Stepper (Installation) bzw. ein Lösungshinweis (Reparatur).</summary>
public sealed class SetupStep(int number, string title, string detail) : ObservableObject
{
    private StepState _state;
    private double _progress;
    private bool _indeterminate;
    private string _detail = detail;
    private bool _isLast;

    public int Number { get; } = number;
    public string Title { get; } = title;
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public StepState State { get => _state; set => Set(ref _state, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public bool Indeterminate { get => _indeterminate; set => Set(ref _indeterminate, value); }

    /// <summary>Letzter Eintrag: keine Verbindungslinie nach unten.</summary>
    public bool IsLast { get => _isLast; set => Set(ref _isLast, value); }
}

/// <summary>
/// Steuert den Einrichtungsassistenten: Download → Prüfung → Installation → Kontrolle.
/// Endet mit <see cref="Completed"/> (Treiber verfügbar) oder <see cref="Dismissed"/> (Benutzer schließt).
/// </summary>
internal sealed class SetupViewModel : ObservableObject
{
    private SetupStage _stage;
    private string _title = "";
    private string _message = "";
    private string? _error;
    private CancellationTokenSource? _cts;
    private int _activeStep = -1;

    public SetupViewModel(DriverInfo driver, bool firstRun)
    {
        InstallSteps = Numbered(
            (Loc.T("StepDownload"), Loc.F("StepDownloadDetail", InstallerSpec.ViGEmBus.Version)),
            (Loc.T("StepVerify"), Loc.T("StepVerifyDetail")),
            (Loc.T("StepInstall"), Loc.T("StepInstallDetail")));
        PrimaryCommand = new RelayCommand(OnPrimary);
        SecondaryCommand = new RelayCommand(OnSecondary);
        LinkCommand = new RelayCommand(OnLink, () => Stage != SetupStage.Working);
        Driver = driver;
        GoTo(firstRun ? SetupStage.Welcome : SetupStage.Repair);
    }

    private static ObservableCollection<SetupStep> Numbered(params (string Title, string Detail)[] items)
    {
        var list = new ObservableCollection<SetupStep>(items.Select((x, i) => new SetupStep(i + 1, x.Title, x.Detail)));
        list[^1].IsLast = true;
        return list;
    }

    public ObservableCollection<SetupStep> InstallSteps { get; }

    /// <summary>Lösungshinweise – nach Wahrscheinlichkeit sortiert, abhängig vom Treiberzustand.</summary>
    public ObservableCollection<SetupStep> RepairSteps => Driver.Status switch
    {
        DriverStatus.NotInstalled => Numbered(
            (Loc.T("HintInstall"), Loc.F("HintInstallDetail", InstallerSpec.ViGEmBus.Version)),
            (Loc.T("HintRebootIfNeeded"), Loc.T("HintRebootIfNeededDetail"))),
        DriverStatus.VersionMismatch => Numbered(
            (Loc.T("HintUpdate"), Loc.F("HintUpdateDetail", InstallerSpec.ViGEmBus.Version)),
            (Loc.T("HintReboot"), Loc.T("HintRebootAfterUpdate"))),
        _ => Numbered(
            (Loc.T("HintReboot"), Loc.T("HintRebootDetail")),
            (Loc.T("HintReinstall"), Loc.T("HintReinstallDetail")),
            (Loc.T("HintSecurity"), Loc.T("HintSecurityDetail"))),
    };

    /// <summary>Stepper (Abfolge) nur für die Installation; Reparatur zeigt Alternativen als neutrale Liste.</summary>
    public bool ShowInstallSteps => Stage is SetupStage.Welcome or SetupStage.Working or SetupStage.Failed;
    public bool ShowHints => Stage == SetupStage.Repair;
    public bool ShowGestures => Stage == SetupStage.Success;

    /// <summary>"Schritt 2 von 3" – nur während der Installation.</summary>
    public string StepCounterText => Stage == SetupStage.Working && _activeStep is >= 0 and < 3
        ? Loc.F("StepCounter", _activeStep + 1, InstallSteps.Count)
        : "";

    public DriverInfo Driver { get; private set; }

    public event Action? Completed;
    public event Action? Dismissed;

    public ICommand PrimaryCommand { get; }
    public ICommand SecondaryCommand { get; }
    public ICommand LinkCommand { get; }

    public SetupStage Stage { get => _stage; private set { if (Set(ref _stage, value)) RaiseStage(); } }

    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public string? Error { get => _error; private set => Set(ref _error, value); }

    public bool IsBusy => Stage == SetupStage.Working;

    /// <summary>Bei "installiert, aber nicht aktiv" ist Neustart + erneut prüfen der wahrscheinlichste Weg.</summary>
    private bool PreferRecheck => Driver.Status is DriverStatus.AccessFailed or DriverStatus.Unknown;

    public string PrimaryText => Stage switch
    {
        SetupStage.Welcome => Loc.T("BtnInstallDriver"),
        SetupStage.Repair => Loc.T(PreferRecheck ? "BtnRecheck" : "BtnInstallDriver"),
        SetupStage.Working => _activeStep switch
        {
            0 => Loc.T("BtnDownloading"),
            1 => Loc.T("BtnVerifying"),
            _ => Loc.T("BtnInstalling"),
        },
        SetupStage.Success => Loc.T("BtnDone"),
        SetupStage.RebootRequired => Loc.T("BtnRebootNow"),
        _ => Loc.T("BtnRetry"),
    };

    public string SecondaryText => Stage switch
    {
        SetupStage.Working => Loc.T("BtnCancel"),
        SetupStage.RebootRequired => Loc.T("BtnLater"),
        SetupStage.Success => "",
        SetupStage.Welcome => Loc.T("BtnCancel"),
        _ => Loc.T("Close"),
    };

    public bool ShowSecondary => SecondaryText.Length > 0;

    /// <summary>Dezenter Text-Link links unten für die jeweils zweitwichtigste Lösung.</summary>
    public string LinkText => Stage switch
    {
        SetupStage.Repair => Loc.T(PreferRecheck ? "HintReinstall" : "BtnRecheck"),
        SetupStage.Failed => Loc.T("BtnRecheck"),
        _ => "",
    };

    public bool ShowLink => LinkText.Length > 0;

    

    /// <summary>Kopf-Symbol: Controller (neutral), Häkchen (Erfolg), Neustart oder Warnung.</summary>
    public string HeroIcon => Stage switch
    {
        SetupStage.Success => "",
        SetupStage.Repair or SetupStage.Failed => "",
        SetupStage.RebootRequired => "",
        _ => "",
    };

    public BadgeKind HeroKind => Stage switch
    {
        SetupStage.Success => BadgeKind.Success,
        SetupStage.Repair or SetupStage.Failed => BadgeKind.Danger,
        SetupStage.RebootRequired => BadgeKind.Warning,
        _ => BadgeKind.Accent,
    };

    private void RaiseStage()
    {
        foreach (var n in new[] { nameof(PrimaryText), nameof(SecondaryText), nameof(ShowSecondary), nameof(LinkText),
                     nameof(ShowLink), nameof(ShowInstallSteps), nameof(ShowHints), nameof(ShowGestures), nameof(StepCounterText), nameof(HeroIcon), nameof(HeroKind), nameof(IsBusy) })
            Raise(n);
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetActiveStep(int index)
    {
        _activeStep = index;
        for (int i = 0; i < InstallSteps.Count; i++)
        {
            var s = InstallSteps[i];
            if (i < index) { s.State = StepState.Done; s.Indeterminate = false; }
            else if (i == index) s.State = StepState.Active;
        }
        Raise(nameof(PrimaryText));
        Raise(nameof(StepCounterText));
    }

    /// <summary>Setzt Stage samt Texten. Öffentlich für den Screenshot-Modus.</summary>
    public void GoTo(SetupStage stage, string? error = null, int activeStep = -1)
    {
        Error = error;
        if (activeStep >= 0) SetActiveStep(activeStep);
        (Title, Message) = stage switch
        {
            SetupStage.Welcome => (Loc.T("SuWelcomeTitle"), Loc.T("SuWelcomeText")),
            SetupStage.Repair => (Loc.T(Driver.Status switch
                {
                    DriverStatus.NotInstalled => "SuRepairMissing",
                    DriverStatus.VersionMismatch => "SuRepairOutdated",
                    _ => "SuRepairInactive",
                }),
                Loc.T("SuRepairText") + " " + Loc.T(PreferRecheck ? "SuRepairInactiveText" : "SuRepairFixText")),
            SetupStage.Working => (Loc.T("SuWorkingTitle"), Loc.T("SuWorkingText")),
            SetupStage.Success => (Loc.T("SuSuccessTitle"), Loc.T("SuSuccessText")),
            SetupStage.RebootRequired => (Loc.T("SuRebootTitle"), Loc.T("SuRebootText")),
            _ => (Loc.T("SuFailedTitle"), Loc.T("SuFailedText")),
        };
        Stage = stage;
        Raise(nameof(PrimaryText));
    }

    private async void OnPrimary()
    {
        switch (Stage)
        {
            case SetupStage.Working:
                return; // läuft bereits
            case SetupStage.Success:
                Completed?.Invoke();
                break;
            case SetupStage.RebootRequired:
                try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false }); }
                catch (Exception ex) { Log.Error("Neustart", ex); }
                Dismissed?.Invoke();
                break;
            case SetupStage.Repair when PreferRecheck:
                Recheck();
                break;
            default:
                await RunAsync();
                break;
        }
    }

    private async void OnLink()
    {
        if (Stage == SetupStage.Repair && PreferRecheck) await RunAsync();
        else Recheck();
    }

    private void OnSecondary()
    {
        if (Stage == SetupStage.Working) { _cts?.Cancel(); return; }
        Dismissed?.Invoke();
    }

    private void Recheck()
    {
        Driver = DriverCheck.Check();
        Raise(nameof(RepairSteps));
        if (Driver.IsOk) GoTo(SetupStage.Success);
        else GoTo(Stage, Loc.T("ErrStillUnavailable") + " " + Driver.Detail);
    }

    private async Task RunAsync()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        foreach (var s in InstallSteps) { s.State = StepState.Pending; s.Progress = 0; s.Indeterminate = false; }
        _activeStep = 0;
        GoTo(SetupStage.Working);

        string? installer = null;
        try
        {
            // 1) Download
            SetActiveStep(0);
            var download = InstallSteps[0];
            installer = await DriverInstaller.DownloadAsync(InstallerSpec.ViGEmBus, new Progress<double>(p => download.Progress = p), ct);

            // 2) Prüfen
            SetActiveStep(1);
            InstallSteps[1].Indeterminate = true;
            await Task.Run(() => DriverInstaller.Verify(InstallerSpec.ViGEmBus, installer), ct);

            // 3) Installieren
            SetActiveStep(2);
            var install = InstallSteps[2];
            install.Indeterminate = true;
            int code = await DriverInstaller.RunElevatedAsync(InstallerSpec.ViGEmBus, installer, ct);
            if (code == DriverInstaller.ExitUserCancel)
                throw new SetupCancelledException(Loc.T("ErrInstallerCancelled"));

            // 4) Kontrolle: Treiber kann einige Sekunden brauchen, bis er erreichbar ist.
            install.Detail = Loc.T("StepChecking");
            DriverInfo info = DriverCheck.Check();
            for (int i = 0; i < 20 && !info.IsOk && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(500, ct);
                info = DriverCheck.Check();
            }
            Driver = info;
            SetActiveStep(3); // alle erledigt
            install.Detail = Loc.T("StepDone");

            if (info.IsOk && code != DriverInstaller.ExitRebootRequired)
            {
                GoTo(SetupStage.Success);
            }
            else if (code is DriverInstaller.ExitRebootRequired or DriverInstaller.ExitRebootInitiated || info.Version is not null)
            {
                Autostart.ResumeAfterReboot();
                GoTo(SetupStage.RebootRequired);
            }
            else
            {
                FailActive();
                GoTo(SetupStage.Failed, Loc.F("ErrInstallerCode", code) + " " + info.Detail);
            }
        }
        catch (OperationCanceledException ex)
        {
            FailActive();
            GoTo(SetupStage.Failed, ex is SetupCancelledException ? ex.Message : Loc.T("ErrCancelled"));
        }
        catch (HttpRequestException ex)
        {
            Log.Error("Download", ex);
            FailActive();
            GoTo(SetupStage.Failed, Loc.T("ErrDownload"));
        }
        catch (InstallerVerificationException ex)
        {
            Log.Error("Verifikation", ex);
            FailActive();
            GoTo(SetupStage.Failed, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error("Einrichtung", ex);
            FailActive();
            GoTo(SetupStage.Failed, ex.Message);
        }
        finally
        {
            DriverInstaller.Cleanup(installer);
            _cts.Dispose();
            _cts = null;
        }
    }

    private void FailActive()
    {
        if (_activeStep is >= 0 and < 3)
        {
            InstallSteps[_activeStep].State = StepState.Failed;
            InstallSteps[_activeStep].Indeterminate = false;
        }
    }
}
