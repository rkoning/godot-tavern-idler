using System.Collections.Generic;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Kernel;

// CON-001 conformance: Outcome result pattern — Success carries events, Failure carries an error,
// and the closed hierarchy pattern-matches exhaustively.
public class OutcomeConformanceTests
{
    private sealed record SampleEvent(int N) : IDomainEvent;

    private static string Describe(Outcome<string> outcome) => outcome switch
    {
        Outcome<string>.Success s => $"ok:{s.Events.Count}",
        Outcome<string>.Failure f => $"err:{f.Error}",
        _ => throw new System.InvalidOperationException("Outcome must be Success or Failure"),
    };

    [Fact]
    public void Success_carries_ordered_events()
    {
        IReadOnlyList<IDomainEvent> events = new IDomainEvent[] { new SampleEvent(1), new SampleEvent(2) };
        var outcome = new Outcome<string>.Success(events);

        Assert.Equal("ok:2", Describe(outcome));
        Assert.Same(events, outcome.Events);
    }

    [Fact]
    public void Failure_carries_error()
    {
        var outcome = new Outcome<string>.Failure("insolvent");
        Assert.Equal("err:insolvent", Describe(outcome));
    }
}
