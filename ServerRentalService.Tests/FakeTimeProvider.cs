namespace ServerRentalService.Tests;

public class FakeTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private DateTimeOffset _current = initialUtc;

    public override DateTimeOffset GetUtcNow() => _current;

    public void Advance(TimeSpan value)
    {
        _current = _current.Add(value);
    }
}
