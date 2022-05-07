namespace Warehouse.Contracts;

public interface AllocationReleaseRequested
{
    Guid AllocationId { get; }

    string Reason { get; }
}
