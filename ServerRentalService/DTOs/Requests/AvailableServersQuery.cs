namespace ServerRentalService.DTOs.Requests;

public class AvailableServersQuery
{
    public string? OperatingSystem { get; set; }
    public int? MinMemoryGb { get; set; }
    public int? MinDiskGb { get; set; }
    public int? MinCpuCores { get; set; }
}
