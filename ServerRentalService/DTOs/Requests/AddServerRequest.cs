using System.ComponentModel.DataAnnotations;

namespace ServerRentalService.DTOs.Requests;

public class AddServerRequest
{
    [Required]
    [MaxLength(128)]
    public string OperatingSystem { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int MemoryGb { get; set; }

    [Range(1, int.MaxValue)]
    public int DiskGb { get; set; }

    [Range(1, int.MaxValue)]
    public int CpuCores { get; set; }

    public bool InitiallyPoweredOn { get; set; }
}
