namespace ServerRentalService.Options;

public class ServerRentalOptions
{
    public const string SectionName = "ServerRental";

    public int BootDurationMinutes { get; set; } = 5;
    public int LeaseDurationMinutes { get; set; } = 20;
    public int LifecycleCheckIntervalSeconds { get; set; } = 5;
    public List<InitialServerOptions> InitialServers { get; set; } = [];
}

public class InitialServerOptions
{
    public string OperatingSystem { get; set; } = string.Empty;
    public int MemoryGb { get; set; }
    public int DiskGb { get; set; }
    public int CpuCores { get; set; }
    public bool InitiallyPoweredOn { get; set; }
}
