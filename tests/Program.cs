using System.Reflection;

// @spec promenade-custom-orders::medium-boss-pool
MethodInfo eligibility = typeof(Plugin).Assembly
    .GetType("ReverseSpeedrunModeState", throwOnError: true)!
    .GetMethod("IsDifficultyEligible", BindingFlags.NonPublic | BindingFlags.Static)!;

AssertEligible(speedrunAvailable: true, speedrunFurierAvailable: false, GameDifficulty.Easy, expected: true);
AssertEligible(speedrunAvailable: true, speedrunFurierAvailable: false, GameDifficulty.Medium, expected: true);
AssertEligible(speedrunAvailable: true, speedrunFurierAvailable: false, GameDifficulty.Hard, expected: false);
AssertEligible(speedrunAvailable: false, speedrunFurierAvailable: true, GameDifficulty.Easy, expected: false);
AssertEligible(speedrunAvailable: false, speedrunFurierAvailable: true, GameDifficulty.Hard, expected: true);

Console.WriteLine("Promenade custom-order acceptance checks passed.");

void AssertEligible(bool speedrunAvailable, bool speedrunFurierAvailable, GameDifficulty difficulty, bool expected)
{
    bool actual = (bool)eligibility.Invoke(null, new object[] { speedrunAvailable, speedrunFurierAvailable, difficulty })!;
    if (actual != expected)
        throw new Exception($"Expected {difficulty} eligibility to be {expected}, got {actual}.");
}
