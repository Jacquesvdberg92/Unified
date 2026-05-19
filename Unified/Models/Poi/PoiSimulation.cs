using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Models.Poi;

public class PoiSimulation
{
    public int    Id         { get; set; }

    /// <summary>The client/account ID that was simulated.</summary>
    public string ClientId   { get; set; } = string.Empty;

    public int    BrandId    { get; set; }
    public Brand? Brand      { get; set; }

    /// <summary>UTC date-time when the simulation was logged.</summary>
    public DateTime SimulatedAt { get; set; } = DateTime.UtcNow;

    public string  LoggedById  { get; set; } = string.Empty;
    public AppUser? LoggedBy   { get; set; }

    /// <summary>Notes / reason for the simulation (optional).</summary>
    public string Notes { get; set; } = string.Empty;

    // ── POI received ──────────────────────────────────────────────────────

    /// <summary>True once a valid ID copy has been received.</summary>
    public bool     PoiReceived   { get; set; }
    public DateTime? ReceivedAt   { get; set; }
    public string?   ReceivedById { get; set; }
    public AppUser?  ReceivedBy   { get; set; }

    // ── Computed helpers (not mapped) ─────────────────────────────────────

    /// <summary>Days since simulation was logged.</summary>
    public int DaysSinceSimulation
        => (int)(DateTime.UtcNow - SimulatedAt).TotalDays;

    /// <summary>
    /// Pending  = POI not yet received, within 3-day grace period.
    /// Restricted = POI not received and 3+ days have elapsed.
    /// Received = valid ID received.
    /// </summary>
    public PoiStatus Status => PoiReceived
        ? PoiStatus.Received
        : DaysSinceSimulation >= 3
            ? PoiStatus.Restricted
            : PoiStatus.Pending;
}

public enum PoiStatus
{
    Pending,
    Restricted,
    Received
}
