namespace Warehouse.Components.StateMachines;

using MassTransit;
using MongoDB.Bson.Serialization.Attributes;


public class AllocationState :
    SagaStateMachineInstance,
    ISagaVersion
{
    public string CurrentState { get; set; } = default!;

    public Guid? HoldDurationToken { get; set; }

    public int Version { get; set; }

    [BsonId]
    public Guid CorrelationId { get; set; }
}
