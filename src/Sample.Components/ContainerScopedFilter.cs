namespace Sample.Components;

using Contracts;
using MassTransit;


public class ContainerScopedFilter :
    IFilter<ConsumeContext<SubmitOrder>>
{
    public Task Send(ConsumeContext<SubmitOrder> context, IPipe<ConsumeContext<SubmitOrder>> next)
    {
        var provider = context.GetPayload<IServiceProvider>();

        Console.WriteLine("Filter ran");

        return next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
    }
}
