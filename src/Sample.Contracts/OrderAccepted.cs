namespace Sample.Contracts;

public interface OrderAccepted
{
    Guid OrderId { get; }
    DateTime Timestamp { get; }
}
