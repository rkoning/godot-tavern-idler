namespace TavernIdler.Domains.Staffing;
using TavernIdler.Kernel;
using TavernIdler.Domains.Structure;   // StaffRequirements (CON-003)

public interface IRoomRequirements
{
    /// Requirements for the room's CURRENT tier (tier overrides applied, CON-004).
    /// Rooms without staffing requirements return an empty Roles list.
    StaffRequirements Get(RoomId room);          // KeyNotFoundException if room absent
    IReadOnlyList<RoomId> RoomsWithRequirements();
}

public interface IHireUnlocks
{
    /// Named hires currently purchasable/hireable (their unlock perk is owned). REQ-063.
    IReadOnlyList<NamedHireId> UnlockedNamedHires();
}
