// Local presentation access only; no money is collected.
sealed class FeatureAccess(IConfiguration config, IHostEnvironment environment) {
    private int simulationActive;
    public bool SimulationActive => Volatile.Read(ref simulationActive) == 1;
    public void ActivateSimulation() => Interlocked.Exchange(ref simulationActive, 1);
    public object Snapshot() => new { plan = Pro ? "pro" : "free", developmentAccess = Pro && !SimulationActive, simulationAccess = SimulationActive, checkoutEnabled = false, monthlyPrice = 29000, currency = "IDR" };
    public bool Pro => SimulationActive || environment.IsDevelopment() && config.GetValue<bool>("Billing:DevelopmentPro");
    public bool Blocks(IEnumerable<Step> steps) => !Pro && steps.Any(s => s.Agent is "drive" or "calendar");
    public static IResult UpgradeRequired() => Results.Json(new {
        code = "upgrade_required", error = "Drive, Calendar, dan Meet termasuk Orbit Pro. Discord tetap gratis."
    }, statusCode: 402);
}
