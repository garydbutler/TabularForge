namespace TabularForge.Core.Models;

public enum AuthenticationType
{
    WindowsIntegrated,
    AzureAD,
    UsernamePassword
}

public enum ServerType
{
    SSAS,
    AzureAS,
    PowerBIXmla
}

public class ConnectionInfo
{
    public string ServerAddress { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public ServerType ServerType { get; set; } = ServerType.SSAS;
    public AuthenticationType AuthType { get; set; } = AuthenticationType.WindowsIntegrated;
    public string DisplayName => string.IsNullOrEmpty(DatabaseName)
        ? ServerAddress
        : $"{ServerAddress}\\{DatabaseName}";
}

public class ServerDatabase
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public long EstimatedSize { get; set; }
    public string CompatibilityLevel { get; set; } = string.Empty;
    public DateTime LastProcessed { get; set; }
}
