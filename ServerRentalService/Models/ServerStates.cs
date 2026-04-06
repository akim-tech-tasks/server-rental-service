namespace ServerRentalService.Models;

public enum ServerPowerState
{
    Off = 0,
    Booting = 1,
    On = 2
}

public enum RentalState
{
    Available = 0,
    PendingBoot = 1,
    Rented = 2
}
