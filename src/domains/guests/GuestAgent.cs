namespace TavernIdler.Domains.Guests;
using TavernIdler.Kernel;

// ── DOM-003: one simulated guest's mutable state ────────────────────────────
// Purely domain data (no engine types). The aggregate GuestPopulation owns and mutates these; they
// are never serialized except as reduced LodgerRecords (Decision C). NextCell/MoveProgress are view
// interpolation data derived from Path, not physics.

internal enum AgentPhase
{
    Selecting,   // choosing / re-pursuing the current agenda want (between wants, or freshly admitted)
    Walking,     // traversing Path toward the current want's room
    Serving,     // occupying a room for the duration of a service
    Blocked,     // waiting at/for a room that can't currently serve the want (REQ-053)
    Lodging,     // completed a Lodging service; parked in its room until next BeginService (REQ-107)
}

internal sealed class GuestAgent
{
    public required GuestId Id { get; init; }
    public required GuestTypeSheet Sheet { get; init; }
    public required bool IsVip { get; init; }
    public required bool AdmittedThisNight { get; set; }

    public Money Wallet { get; set; }
    public double Satisfaction { get; set; }          // [-1, +1]

    public CellCoord Cell { get; set; }
    public List<CellCoord> Path { get; set; } = new(); // remaining cells to walk (excludes Cell)
    public double MoveProgress { get; set; }           // [0,1) toward Path[0]

    public int AgendaIndex { get; set; }
    public AgentPhase Phase { get; set; } = AgentPhase.Selecting;

    public RoomId? Room { get; set; }                  // the room being walked-to / served-in / lodged-in
    public string? ServiceId { get; set; }             // the service currently being performed
    public bool ServingLodging { get; set; }
    public MenuItemId? Consuming { get; set; }         // non-null during MenuConsumption service (DOM006-Q1)
    public int ServiceRemaining { get; set; }

    public BlockReason? CurrentBlock { get; set; }
    public int BlockedWaitRemaining { get; set; }

    // Effect state (CON-011 via ApplyEffects). Keyed by EpisodeId; removed by the matching …Ended.
    public Dictionary<long, double> SpendingEpisodes { get; } = new();      // multiplicative
    public Dictionary<long, double> SatisfactionDrifts { get; } = new();    // additive per tick
    public List<Burst> Bursts { get; } = new();                             // one-shot spending bursts

    public GuestAgentView ToView() =>
        new(Id, Sheet.Id, IsVip, Cell,
            Phase == AgentPhase.Walking && Path.Count > 0 ? Path[0] : null,
            Phase == AgentPhase.Walking && Path.Count > 0 ? MoveProgress : 0.0,
            Activity, Satisfaction, Sheet.Traits);

    public GuestActivity Activity => Phase switch
    {
        AgentPhase.Serving => GuestActivity.BeingServed,
        AgentPhase.Blocked => GuestActivity.WaitingBlocked,
        AgentPhase.Lodging => GuestActivity.Lodging,
        _ => GuestActivity.Walking,
    };

    /// The room this guest physically occupies right now (Serving/Lodging), else null (walking).
    public RoomId? OccupiedRoom => Phase is AgentPhase.Serving or AgentPhase.Lodging ? Room : null;

    public double SpendingMultiplier
    {
        get
        {
            var m = 1.0;
            foreach (var f in SpendingEpisodes.Values) m *= f;
            foreach (var b in Bursts) m *= b.Factor;
            return m;
        }
    }

    /// clamp(1 + 0.5·satisfaction, 0.5, 1.5) — CON-005 REQ-023.
    public double SatisfactionModifier => System.Math.Clamp(1.0 + 0.5 * Satisfaction, 0.5, 1.5);
}

/// A one-shot spending multiplier active until <see cref="ExpiryElapsed"/> (absolute service-elapsed tick).
internal readonly record struct Burst(double Factor, int ExpiryElapsed);
