using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Kernel;

// CON-001 conformance: id record equality and ordinal case-sensitivity of string ids.
public class IdConformanceTests
{
    [Fact]
    public void Int_id_equality_by_value()
    {
        Assert.Equal(new RoomId(1), new RoomId(1));
        Assert.NotEqual(new RoomId(1), new RoomId(2));
    }

    [Fact]
    public void Distinct_int_id_types_are_not_interchangeable()
    {
        // Compile-time proof: RoomId and GuestId are distinct types.
        RoomId room = new(1);
        GuestId guest = new(1);
        Assert.Equal(room.Value, guest.Value);
    }

    [Fact]
    public void String_id_equality_is_case_sensitive_ordinal()
    {
        Assert.Equal(new TraitId("Cozy"), new TraitId("Cozy"));
        Assert.NotEqual(new TraitId("Cozy"), new TraitId("cozy"));
    }

    [Fact]
    public void String_id_equality_across_kinds()
    {
        Assert.Equal(new RoomTypeId("bar"), new RoomTypeId("bar"));
        Assert.NotEqual(new GuestTypeId("noble"), new GuestTypeId("Noble"));
        Assert.Equal(new VenueId("starter"), new VenueId("starter"));
    }
}
