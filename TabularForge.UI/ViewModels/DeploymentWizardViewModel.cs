using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class DeploymentWizardViewModel : ObservableObject
{
    private readonly DeploymentService _deploymentService;
    private readonly ConnectionService _connectionService;
    private TomNode? _modelRoot;

    // === Wizard Step ===
    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private string _stepTitle = "Step 1: Target Server";

    // === Step 1: Server ===
    [ObservableProperty]
    private string _targetServerAddress = string.Empty;

    [ObservableProperty]
    private AuthenticationType _targetAuthType = AuthenticationType.WindowsIntegrated;

    // === Step 2: Database ===
    [ObservableProperty]
    private string _targetDatabaseName = string.Empty;

    [ObservableProperty]
    private bool _createNewDatabase;

    [ObservableProperty]
    private bool _overwriteExisting = true;

    // === Step 3: Options ===
    [ObservableProperty]
    private DeploymentMode _deploymentMode = DeploymentMode.MetadataOnly;

    // === Step 4: Validation ===
    [ObservableProperty]
    private ObservableCollection<string> _validationErrors = new();

    [ObservableProperty]
    private ObservableCollection<string> _validationWarnings = new();

    [ObservableProperty]
    private bool _isValidated;

    // === Step 5: Script Preview ===
    [ObservableProperty]
    private string _tmslScript = string.Empty;

    // === Step 6: Deploy ===
    [ObservableProperty]
    private bool _isDeploying;

    [ObservableProperty]
    private string _deploymentStatus = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _deploymentLog = new();

    [ObservableProperty]
    private bool _deploymentComplete;

    [ObservableProperty]
    private bool _deploymentSucceeded;

    public bool DialogResult { get; private set; }
    public Array DeploymentModes => Enum.GetValues(typeof(DeploymentMode));
    public Array AuthenticationTypes => Enum.GetValues(typeof(AuthenticationType));

    public event EventHandler<string>? MessageLogged;

    public DeploymentWizardViewModel(DeploymentService deploymentService, ConnectionService connectionService)
    {
        _deploymentService = deploymentService;
        _connectionService = connectionService;

        _deploymentService.DeploymentProgress += (_, message) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DeploymentLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                DeploymentStatus = message;
            });
        };
    }

    public void Initialize(TomNode? modelRoot)
    {
        _modelRoot = modelRoot;
        CurrentStep = 0;
        UpdateStepTitle();
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < 5)
        {
            CurrentStep++;
            UpdateStepTitle();

            // Auto-actions per step
            if (CurrentStep == 3)
                ValidateDeployment();
            if (CurrentStep == 4)
                GenerateScript();
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            UpdateStepTitle();
        }
    }

    private void ValidateDeployment()
    {
        var options = BuildOptions();
        var result = _deploymentService.ValidateDeployment(options, _modelRoot);

        ValidationErrors.Clear();
        ValidationWarnings.Clear();

        foreach (var err in result.Errors)
            ValidationErrors.Add(err);
        foreach (var warn in result.Warnings)
            ValidationWarnings.Add(warn);

        IsValidated = result.IsValid;
    }

    private void GenerateScript()
    {
        if (_modelRoot == null) return;
        TmslScript = _deploymentService.GenerateTmslScript(BuildOptions(), _modelRoot);
    }

    [RelayCommand]
    private async Task Deploy()
    {
        if (_modelRoot == null) return;

        IsDeploying = true;
        DeploymentComplete = false;
        DeploymentLog.Clear();

        try
        {
            var options = BuildOptions();
            var (success, message) = await _deploymentService.DeployAsync(options, _modelRoot);

            DeploymentSucceeded = success;
            DeploymentComplete = true;
            DeploymentStatus = message;
            DeploymentLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            MessageLogged?.Invoke(this, message);

            if (success)
                DialogResult = true;
        }
        catch (Exception ex)
        {
            DeploymentSucceeded = false;
            DeploymentComplete = true;
            DeploymentStatus = $"Error: {ex.Message}";
            DeploymentLog.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        finally
        {
            IsDeploying = false;
        }
    }

    private DeploymentOptions BuildOptions()
    {
        return new DeploymentOptions
        {
            TargetServer = new ConnectionInfo
            {
                ServerAddress = TargetServerAddress,
                AuthType = TargetAuthType
            },
            TargetDatabaseName = TargetDatabaseName,
            Mode = DeploymentMode,
            CreateNewDatabase = CreateNewDatabase,
            OverwriteExisting = OverwriteExisting
        };
    }

    private void UpdateStepTitle()
    {
        StepTitle = CurrentStep switch
        {
            0 => "Step 1: Target Server",
            1 => "Step 2: Target Database",
            2 => "Step 3: Deployment Options",
            3 => "Step 4: Validation",
            4 => "Step 5: TMSL Script Preview",
            5 => "Step 6: Deploy",
            _ => "Deployment Wizard"
        };
    }
}
