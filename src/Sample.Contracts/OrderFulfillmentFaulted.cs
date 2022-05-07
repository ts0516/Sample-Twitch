namespace Sample.Contracts;

public interface OrderFulfillmentFaulted
{
    Guid OrderId { get; }

    DateTime Timestamp { get; }
}
