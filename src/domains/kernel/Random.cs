namespace TavernIdler.Kernel;

public interface IRandomSource
{
    /// Named, independent deterministic streams. Same (seed, name) ⇒ same sequence,
    /// regardless of what other streams consumed.
    IRandom GetStream(string name);
    long Seed { get; }
}

public interface IRandom
{
    double NextDouble();               // uniform [0.0, 1.0)
    int NextInt(int maxExclusive);     // uniform [0, maxExclusive); maxExclusive ≥ 1
}
