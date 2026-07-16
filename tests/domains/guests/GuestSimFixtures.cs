using TavernIdler.Adapters.Random;           // DeterministicRandomSource (CON-015 impl, TKT-017)
using TavernIdler.Tests.Contracts.Guests;    // abstract suite, GuestWorld, IGuestSimTestHarness
using Domain = TavernIdler.Domains.Guests;   // real guest simulation

namespace TavernIdler.Tests.Domains.Guests;

/// <summary>
/// Wires the real <see cref="Domain.GuestPopulation"/> to the seam the CON-005 behavioral conformance
/// suite (TKT-006) drives. The simulation is seeded from <see cref="GuestWorld.Seed"/> via the real
/// CON-015 RNG adapter and reads the four CON-006 driven-port doubles the suite supplies.
///
/// Determinism holds because equal <see cref="GuestWorld"/>s (same seed) build identical "guests"
/// streams and inject the same movement constant. The harness NEVER reseeds across nights (G4): the
/// domain draws continuously, so the 1000-night VIP frequency test sees a fresh sequence each night.
/// A fixed small <c>guestTicksPerCell</c> is injected here (G3): GuestWorld carries no movement
/// constant, and equal worlds ⇒ equal constant ⇒ determinism.
/// </summary>
public sealed class GuestSimHarness : IGuestSimTestHarness
{
    private const int GuestTicksPerCell = 1;   // fixed small movement constant (G3)

    private readonly Domain.GuestPopulation _population;

    public GuestSimHarness(GuestWorld world) =>
        _population = new Domain.GuestPopulation(
            GuestCatalogMapping.ToDomain(world.Catalog),
            world.ServiceDurationTicks,
            GuestTicksPerCell,
            world.Structure,
            world.RoomServices,
            world.Transactions,
            world.Attraction,
            new DeterministicRandomSource(world.Seed, nightNumber: 1));

    public Domain.IGuestSimCommands Commands => _population;
    public Domain.IGuestView View => _population;
    public Domain.IGuestPresence Presence => _population;
}

/// <summary>Runs the CON-005 behavioral conformance suite against the real guest simulation.</summary>
public sealed class GuestSimConformance : GuestSimConformanceTests
{
    protected override IGuestSimTestHarness CreateSut(GuestWorld world) => new GuestSimHarness(world);
}
