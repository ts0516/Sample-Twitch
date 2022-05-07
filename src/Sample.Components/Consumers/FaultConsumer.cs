namespace Sample.Components.Consumers;

using Contracts;
using MassTransit;


public class FaultConsumer :
    IConsumer<Fault<FulfillOrder>>
{
    public Task Consume(ConsumeContext<Fault<FulfillOrder>> context)
    {
        return Task.CompletedTask;
    }
}
