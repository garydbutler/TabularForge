namespace TabularForge.Core.Models;

public enum DeploymentMode
{
    MetadataOnly,
    MetadataAndData,
    DataOnly
}

public class DeploymentOptions
{
    public ConnectionInfo TargetServer { get; set; } = new();
    public string TargetDatabaseName { get; set; } = string.Empty;
    public DeploymentMode Mode { get; set; } = DeploymentMode.MetadataOnly;
    public bool CreateNewDatabase { get; set; }
    public bool OverwriteExisting { get; set; } = true;
}

public class DeploymentValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
