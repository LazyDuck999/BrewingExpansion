using System;

namespace BrewingExpansion;

internal static class SeedOddsCalculator
{
    private const int RemoteBasisPoints = 1500;
    private const int MortalFloorBasisPoints = 5000;

    // Probabilities use basis points (1/100 of a percent).
    internal static (int mortal, int remote, int longing, int afterglow) Calculate(
        int longingMisses, int afterglowMisses, int certLevel)
    {
        int multiplierTenths = certLevel >= 3 ? 16 : certLevel >= 2 ? 13 : 10;
        int longingBase = 300 + Math.Min(Math.Max(0, longingMisses) / 10, 5) * 100;
        int afterglowBase = 100 + Math.Min(Math.Max(0, afterglowMisses) / 20, 8) * 50;
        int longing = longingBase * multiplierTenths / 10;
        int afterglow = afterglowBase * multiplierTenths / 10;
        int mortal = 10000 - RemoteBasisPoints - longing - afterglow;

        // Future odds changes must still leave at least half the draws Mortal.
        if (mortal < MortalFloorBasisPoints)
        {
            int rareBudget = 10000 - MortalFloorBasisPoints - RemoteBasisPoints;
            int rareTotal = longing + afterglow;
            longing = longing * rareBudget / rareTotal;
            afterglow = rareBudget - longing;
            mortal = MortalFloorBasisPoints;
        }
        return (mortal, RemoteBasisPoints, longing, afterglow);
    }

    // rarity: Mortal=0, From Afar=1, Longing=2, Afterglow=3.
    internal static (int longingMisses, int afterglowMisses) Advance(
        int longingMisses, int afterglowMisses, int rarity)
    {
        if (rarity == 3) return (0, 0);
        if (rarity == 2) return (0, Increase(afterglowMisses));
        return (Increase(longingMisses), Increase(afterglowMisses));
    }

    private static int Increase(int misses) => misses < int.MaxValue ? Math.Max(0, misses) + 1 : misses;
}
