// Local presentation access only; no money is collected.
sealed class FeatureAccess(IConfiguration config, IHostEnvironment environment, OrbitDatabase? database = null)
{
    private int simulationActive;
    public bool SimulationActive => Volatile.Read(ref simulationActive) == 1;
    public void ActivateSimulation() => Interlocked.Exchange(ref simulationActive, 1);
    public object Snapshot() => new { plan = Pro ? "pro" : "free", developmentAccess = Pro && !SimulationActive, simulationAccess = SimulationActive, checkoutEnabled = false, monthlyPrice = 29000, currency = "IDR" };
    public bool Pro => SimulationActive || environment.IsDevelopment() && config.GetValue<bool>("Billing:DevelopmentPro");
    public bool Blocks(IEnumerable<Step> steps, string? owner = null) => !(database?.Enabled == true ? owner is not null && database.TrialActive(owner) : Pro) && steps.Any(s => s.Agent is "drive" or "calendar" or "docs" or "gmail");
    public static IResult UpgradeRequired() => Results.Json(new
    {
        code = "upgrade_required",
        error = "Drive, Docs, Gmail, Calendar, dan Meet termasuk Orbit Pro. Discord tetap gratis."
    }, statusCode: 402);
}
