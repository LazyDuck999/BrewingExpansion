using BrewingExpansion;

static void Odds(int longingMisses, int afterglowMisses, int cert,
    int mortal, int remote, int longing, int afterglow)
{
    var actual = SeedOddsCalculator.Calculate(longingMisses, afterglowMisses, cert);
    if (actual != (mortal, remote, longing, afterglow))
        throw new Exception($"Odds ({longingMisses}, {afterglowMisses}, {cert}): {actual}");
}

static void Counts(int longingMisses, int afterglowMisses, int rarity, int longing, int afterglow)
{
    var actual = SeedOddsCalculator.Advance(longingMisses, afterglowMisses, rarity);
    if (actual != (longing, afterglow))
        throw new Exception($"Counters ({longingMisses}, {afterglowMisses}, {rarity}): {actual}");
}

Odds(0, 0, 0, 8100, 1500, 300, 100);
Odds(0, 0, 1, 8100, 1500, 300, 100);
Odds(9, 19, 0, 8100, 1500, 300, 100);
Odds(10, 20, 0, 7950, 1500, 400, 150);
Odds(50, 160, 0, 7200, 1500, 800, 500);
Odds(0, 0, 2, 7980, 1500, 390, 130);
Odds(50, 160, 2, 6810, 1500, 1040, 650);
Odds(0, 0, 3, 7860, 1500, 480, 160);
Odds(50, 160, 3, 6420, 1500, 1280, 800);
Odds(10000, 10000, 3, 6420, 1500, 1280, 800);

Counts(10, 20, 0, 11, 21);
Counts(10, 20, 1, 11, 21);
Counts(10, 20, 2, 0, 21);
Counts(10, 20, 3, 0, 0);
Counts(int.MaxValue, int.MaxValue, 0, int.MaxValue, int.MaxValue);

for (int cert = 0; cert <= 3; cert++)
    for (int longingMisses = 0; longingMisses <= 60; longingMisses++)
        for (int afterglowMisses = 0; afterglowMisses <= 180; afterglowMisses++)
        {
            var p = SeedOddsCalculator.Calculate(longingMisses, afterglowMisses, cert);
            if (p.mortal + p.remote + p.longing + p.afterglow != 10000
                || p.mortal < 5000 || p.remote != 1500)
                throw new Exception($"Invalid distribution at ({longingMisses}, {afterglowMisses}, {cert}): {p}");
        }

Console.WriteLine("Seed odds and counter checks passed.");
