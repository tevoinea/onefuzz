using Microsoft.OneFuzz.Service.OneFuzzLib.Orm;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Region = System.String;
using PoolName = System.String;
using Endpoint = System.String;
using GroupId = System.Guid;
using PrincipalId = System.Guid;

namespace Microsoft.OneFuzz.Service;

/// Convention for database entities:
/// All entities are represented by immutable records
/// All database entities need to derive from EntityBase
/// Only properties that also apears as parameter initializers are mapped to the database
/// The name of the property will be tranlated to snake case and used as the column name
/// It is possible to rename the column name by using the [property:JsonPropertyName("column_name")] attribute
/// the "partion key" and "row key" are identified by the [PartitionKey] and [RowKey] attributes
/// Guids are mapped to string in the db


public record Authentication
(
    string Password,
    string PublicKey,
    string PrivateKey
);

[SkipRename]
public enum HeartbeatType
{
    MachineAlive,
    TaskAlive,
}

public record HeartbeatData(HeartbeatType Type);

public record TaskHeartbeatEntry(
    Guid TaskId,
    Guid? JobId,
    Guid MachineId,
    HeartbeatData[] Data
    );
public record NodeHeartbeatEntry(Guid NodeId, HeartbeatData[] Data);

public record NodeCommandStopIfFree();

public record StopNodeCommand();

public record StopTaskNodeCommand(Guid TaskId);

public record NodeCommandAddSshKey(string PublicKey);


public record NodeCommand
(
    StopNodeCommand? Stop,
    StopTaskNodeCommand? StopTask,
    NodeCommandAddSshKey? AddSshKey,
    NodeCommandStopIfFree? StopIfFree
);

public enum NodeTaskState
{
    Init,
    SettingUp,
    Running,
}

public record NodeTasks
(
    Guid MachineId,
    Guid TaskId,
    NodeTaskState State = NodeTaskState.Init
);

public enum NodeState
{
    Init,
    Free,
    SettingUp,
    Rebooting,
    Ready,
    Busy,
    Done,
    Shutdown,
    Halt,
}

public record ProxyHeartbeat
(
    Region Region,
    Guid ProxyId,
    List<ProxyForward> Forwards,
    DateTimeOffset TimeStamp
);

public partial record Node
(
    DateTimeOffset? InitializedAt,
    [PartitionKey] PoolName PoolName,
    Guid? PoolId,
    [RowKey] Guid MachineId,
    NodeState State,
    Guid? ScalesetId,
    DateTimeOffset Heartbeat,
    string Version,
    bool ReimageRequested,
    bool DeleteRequested,
    bool DebugKeepNode
) : EntityBase();


public partial record ProxyForward
(
    [PartitionKey] Region Region,
    [RowKey] int DstPort,
    int SrcPort,
    string DstIp
) : EntityBase();

public partial record ProxyConfig
(
    Uri Url,
    string Notification,
    Region Region,
    Guid? ProxyId,
    List<ProxyForward> Forwards,
    string InstanceTelemetryKey,
    string MicrosoftTelemetryKey

);

public partial record Proxy
(
    [PartitionKey] Region Region,
    [RowKey] Guid ProxyId,
    DateTimeOffset? CreatedTimestamp,
    VmState State,
    Authentication Auth,
    string? Ip,
    Error? Error,
    string Version,
    ProxyHeartbeat? Heartbeat
) : EntityBase();

public record Error(ErrorCode Code, string[]? Errors = null);

public record UserInfo(Guid? ApplicationId, Guid? ObjectId, String? Upn);




public record TaskDetails(

    TaskType Type,
    int Duration,
    string? TargetExe,
    Dictionary<string, string>? TargetEnv,
    List<string>? TargetOptions,
    int? TargetWorkers,
    bool? TargetOptionsMerge,
    bool? CheckAsanLog,
    bool? CheckDebugger,
    int? CheckRetryCount,
    bool? CheckFuzzerHelp,
    bool? ExpectCrashOnFailure,
    bool? RenameOutput,
    string? SupervisorExe,
    Dictionary<string, string>? SupervisorEnv,
    List<string>? SupervisorOptions,
    string? SupervisorInputMarker,
    string? GeneratorExe,
    Dictionary<string, string>? GeneratorEnv,
    List<string>? GeneratorOptions,
    string? AnalyzerExe,
    Dictionary<string, string>? AnalyzerEnv,
    List<string> AnalyzerOptions,
    ContainerType? WaitForFiles,
    string? StatsFile,
    StatsFormat? StatsFormat,
    bool? RebootAfterSetup,
    int? TargetTimeout,
    int? EnsembleSyncDelay,
    bool? PreserveExistingOutputs,
    List<string>? ReportList,
    int? MinimizedStackDepth,
    string? CoverageFilter
);

public record TaskVm(
    Region Region,
    string Sku,
    string Image,
    int Count,
    bool SpotInstance,
    bool? RebootAfterSetup
);

public record TaskPool(
    int Count,
    PoolName PoolName
);

public record TaskContainers(
    ContainerType Type,
    Container Name
);
public record TaskConfig(
   Guid JobId,
   List<Guid>? PrereqTasks,
   TaskDetails Task,
   TaskVm? Vm,
   TaskPool? Pool,
   List<TaskContainers>? Containers,
   Dictionary<string, string>? Tags,
   List<TaskDebugFlag>? Debug,
   bool? Colocate
   );


public record TaskEventSummary(
    DateTimeOffset? Timestamp,
    string EventData,
    string EventType
    );


public record NodeAssignment(
    Guid NodeId,
    Guid? ScalesetId,
    NodeTaskState State
    );


public record Task(
    // Timestamp: Optional[datetime] = Field(alias="Timestamp")
    [PartitionKey] Guid JobId,
    [RowKey] Guid TaskId,
    TaskState State,
    Os Os,
    TaskConfig Config,
    Error? Error,
    Authentication? Auth,
    DateTimeOffset? Heartbeat,
    DateTimeOffset? EndTime,
    UserInfo? UserInfo) : EntityBase()
{
    List<TaskEventSummary> Events { get; set; } = new List<TaskEventSummary>();
    List<NodeAssignment> Nodes { get; set; } = new List<NodeAssignment>();
}
public record AzureSecurityExtensionConfig();
public record GenevaExtensionConfig();


public record KeyvaultExtensionConfig(
    string KeyVaultName,
    string CertName,
    string CertPath,
    string ExtensionStore
);

public record AzureMonitorExtensionConfig(
    string ConfigVersion,
    string Moniker,
    string Namespace,
    [property: JsonPropertyName("monitoringGSEnvironment")] string MonitoringGSEnvironment,
    [property: JsonPropertyName("monitoringGCSAccount")] string MonitoringGCSAccount,
    [property: JsonPropertyName("monitoringGCSAuthId")] string MonitoringGCSAuthId,
    [property: JsonPropertyName("monitoringGCSAuthIdType")] string MonitoringGCSAuthIdType
);

public record AzureVmExtensionConfig(
    KeyvaultExtensionConfig? Keyvault,
    AzureMonitorExtensionConfig AzureMonitor
);

public record NetworkConfig(
    string AddressSpace,
    string Subnet
)
{
    public NetworkConfig() : this("10.0.0.0/8", "10.0.0.0/16") { }
}

public record NetworkSecurityGroupConfig(
    string[] AllowedServiceTags,
    string[] AllowedIps
)
{
    public NetworkSecurityGroupConfig() : this(Array.Empty<string>(), Array.Empty<string>()) { }
}

public record ApiAccessRule(
    string[] Methods,
    Guid[] AllowedGroups
);

public record InstanceConfig
(
    [PartitionKey, RowKey] string InstanceName,
    //# initial set of admins can only be set during deployment.
    //# if admins are set, only admins can update instance configs.
    Guid[]? Admins,
    //# if set, only admins can manage pools or scalesets
    bool AllowPoolManagement,
    string[] AllowedAadTenants,
    NetworkConfig NetworkConfig,
    NetworkSecurityGroupConfig ProxyNsgConfig,
    AzureVmExtensionConfig? Extensions,
    string ProxyVmSku,
    IDictionary<Endpoint, ApiAccessRule>? ApiAccessRules,
    IDictionary<PrincipalId, GroupId[]>? GroupMembership,

    IDictionary<string, string>? VmTags,
    IDictionary<string, string>? VmssTags
) : EntityBase()
{
    public InstanceConfig(string instanceName) : this(
        instanceName,
        null,
        true,
        Array.Empty<string>(),
        new NetworkConfig(),
        new NetworkSecurityGroupConfig(),
        null,
        "Standard_B2s",
        null,
        null,
        null,
        null)
    { }

    public InstanceConfig() : this(String.Empty) { }

    public List<Guid>? CheckAdmins(List<Guid>? value)
    {
        if (value is not null && value.Count == 0)
        {
            throw new ArgumentException("admins must be null or contain at least one UUID");
        }
        else
        {
            return value;
        }
    }


    //# At the moment, this only checks allowed_aad_tenants, however adding
    //# support for 3rd party JWT validation is anticipated in a future release.
    public ResultOk<List<string>> CheckInstanceConfig()
    {
        List<string> errors = new();
        if (AllowedAadTenants.Length == 0)
        {
            errors.Add("allowed_aad_tenants must not be empty");
        }
        if (errors.Count == 0)
        {
            return ResultOk<List<string>>.Ok();
        }
        else
        {
            return ResultOk<List<string>>.Error(errors);
        }
    }
}


public record ScalesetNodeState(
    Guid MachineId,
    string InstanceId,
    NodeState? State

);


public record Scaleset(
    [PartitionKey] PoolName PoolName,
    [RowKey] Guid ScalesetId,
    ScalesetState State,
    Authentication? Auth,
    string VmSku,
    string Image,
    Region Region,
    int Size,
    bool SpotInstance,
    bool EphemeralOsDisks,
    bool NeedsConfigUpdate,
    List<ScalesetNodeState> Nodes,
    Guid? ClientId,
    Guid? ClientObjectId,
    Dictionary<string, string> Tags

) : EntityBase();

public record Container(string ContainerName)
{
    public string ContainerName { get; } = ContainerName.All(c => char.IsLetterOrDigit(c) || c == '-') ? ContainerName : throw new ArgumentException("Container name must have only numbers, letters or dashes");
}

public record Notification(
    DateTime? Timestamp,
    Container Container,
    Guid NotificationId,
    NotificationTemplate Config
) : EntityBase();

public record BlobRef(
    string Account,
    Container container,
    string name
);

public record Report(
    string? InputUrl,
    BlobRef? InputBlob,
    string Executable,
    string CrashType,
    string CrashSite,
    IEnumerable<string> CallStack,
    string CallStackSha256,
    string InputSha256,
    string? AsanLog,
    Guid TaskId,
    Guid JobId,
    int? ScarinessScore,
    string? ScarinessDescription,
    IEnumerable<string>? MinimizedStack,
    string? MinimizedStackSha256,
    IEnumerable<string>? MinimizedStackFunctionNames,
    string? MinimizedStackFunctionNamesSha256,
    IEnumerable<string>? MinimizedStackFunctionLines,
    string? MinimizedStackFunctionLinesSha256
);

public record NoReproReport(
    string InputSha,
    BlobRef? InputBlob,
    string? Executable,
    Guid TaskId,
    Guid JobId,
    int Tries,
    string? Error
);

public record CrashTestResult(
    Report? CrashReport,
    NoReproReport? NoReproReport
);

public record RegressionReport(
    CrashTestResult CrashTestResult,
    CrashTestResult? OriginalCrashTestResult
);

public record NotificationTemplate(
    AdoTemplate? AdoTemplate,
    TeamsTemplate? TeamsTemplate,
    GithubIssuesTemplate? GithubIssuesTemplate
);

public record AdoTemplate();

public record TeamsTemplate();

public record GithubIssuesTemplate();
