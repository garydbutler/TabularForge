using Microsoft.AnalysisServices.Tabular;
using Newtonsoft.Json.Linq;
using TabularForge.Core.Models;
using TomDatabase = Microsoft.AnalysisServices.Tabular.Database;
using TomServer = Microsoft.AnalysisServices.Tabular.Server;

namespace TabularForge.Core.Services;

public class DeploymentService
{
    private readonly ConnectionService _connectionService;
    private readonly BimFileService _bimFileService;

    public event EventHandler<string>? DeploymentProgress;

    public DeploymentService(ConnectionService connectionService, BimFileService bimFileService)
    {
        _connectionService = connectionService;
        _bimFileService = bimFileService;
    }

    public DeploymentValidationResult ValidateDeployment(DeploymentOptions options, TomNode? modelRoot)
    {
        var result = new DeploymentValidationResult { IsValid = true };

        if (modelRoot == null)
        {
            result.IsValid = false;
            result.Errors.Add("No model is loaded.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(options.TargetServer.ServerAddress))
        {
            result.IsValid = false;
            result.Errors.Add("Target server address is required.");
        }

        if (string.IsNullOrWhiteSpace(options.TargetDatabaseName))
        {
            result.IsValid = false;
            result.Errors.Add("Target database name is required.");
        }

        if (options.Mode == DeploymentMode.DataOnly && !_connectionService.IsConnected)
        {
            result.Warnings.Add("Data-only deployment requires a source server connection for data refresh.");
        }

        return result;
    }

    public string GenerateTmslScript(DeploymentOptions options, TomNode modelRoot)
    {
        var json = _bimFileService.GetModelJson(modelRoot);
        if (json == null) return "// Error: Could not generate model JSON";

        if (options.CreateNewDatabase)
        {
            var createScript = new JObject
            {
                ["createOrReplace"] = new JObject
                {
                    ["object"] = new JObject
                    {
                        ["database"] = options.TargetDatabaseName
                    },
                    ["database"] = new JObject
                    {
                        ["name"] = options.TargetDatabaseName,
                        ["model"] = json["model"]
                    }
                }
            };
            return createScript.ToString(Newtonsoft.Json.Formatting.Indented);
        }
        else
        {
            var alterScript = new JObject
            {
                ["createOrReplace"] = new JObject
                {
                    ["object"] = new JObject
                    {
                        ["database"] = options.TargetDatabaseName
                    },
                    ["database"] = new JObject
                    {
                        ["name"] = options.TargetDatabaseName,
                        ["model"] = json["model"]
                    }
                }
            };
            return alterScript.ToString(Newtonsoft.Json.Formatting.Indented);
        }
    }

    public async Task<(bool Success, string Message)> DeployAsync(
        DeploymentOptions options, TomNode modelRoot, CancellationToken cancellationToken = default)
    {
        var validation = ValidateDeployment(options, modelRoot);
        if (!validation.IsValid)
        {
            return (false, "Validation failed: " + string.Join("; ", validation.Errors));
        }

        try
        {
            DeploymentProgress?.Invoke(this, "Connecting to target server...");

            using var targetServer = new TomServer();
            var connStr = $"Provider=MSOLAP;Data Source={options.TargetServer.ServerAddress};Integrated Security=SSPI;";
            await Task.Run(() => targetServer.Connect(connStr), cancellationToken);

            DeploymentProgress?.Invoke(this, "Connected. Preparing deployment...");

            var tmslScript = GenerateTmslScript(options, modelRoot);

            DeploymentProgress?.Invoke(this, "Executing deployment script...");

            await Task.Run(() =>
            {
                var results = targetServer.Execute(tmslScript);
                if (results.Count > 0)
                {
                    // Check for errors in results
                    foreach (Microsoft.AnalysisServices.XmlaResult result in results)
                    {
                        foreach (Microsoft.AnalysisServices.XmlaMessage msg in result.Messages)
                        {
                            if (msg is Microsoft.AnalysisServices.XmlaError)
                                throw new Exception($"Deployment error: {msg.Description}");
                        }
                    }
                }
            }, cancellationToken);

            if (options.Mode == DeploymentMode.MetadataAndData)
            {
                DeploymentProgress?.Invoke(this, "Processing data refresh...");

                await Task.Run(() =>
                {
                    var db = targetServer.Databases.FindByName(options.TargetDatabaseName);
                    if (db?.Model != null)
                    {
                        db.Model.RequestRefresh(Microsoft.AnalysisServices.Tabular.RefreshType.Full);
                        db.Model.SaveChanges();
                    }
                }, cancellationToken);
            }

            DeploymentProgress?.Invoke(this, "Deployment completed successfully.");
            return (true, "Deployment completed successfully.");
        }
        catch (OperationCanceledException)
        {
            return (false, "Deployment was cancelled.");
        }
        catch (Exception ex)
        {
            return (false, $"Deployment failed: {ex.Message}");
        }
    }
}
