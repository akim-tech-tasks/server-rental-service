namespace ServerRentalService.Services;

public enum ServiceError
{
    None = 0,
    NotFound = 1,
    Conflict = 2
}

public record ServiceResult(ServiceError Error = ServiceError.None)
{
    public bool Success => Error == ServiceError.None;

    public static ServiceResult Ok() => new();
    public static ServiceResult Fail(ServiceError error) => new(error);
}

public record ServiceResult<T>(T? Value, ServiceError Error = ServiceError.None)
{
    public bool Success => Error == ServiceError.None;

    public static ServiceResult<T> Ok(T value) => new(value);
    public static ServiceResult<T> Fail(ServiceError error) => new(default, error);
}
