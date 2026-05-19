using Unified.Models.Identity;

namespace Unified.Models.Attendance;

public class AttendanceLog
{
    public int Id { get; set; }

    // ── Agent ──────────────────────────────────────────────────────────────
    public string AgentId { get; set; } = string.Empty;
    public AppUser? Agent { get; set; }

    // ── Times ──────────────────────────────────────────────────────────────
    /// <summary>Date this log belongs to (date-only portion).</summary>
    public DateTime WorkDate { get; set; }

    public DateTime? CheckInTime  { get; set; }
    public DateTime? CheckOutTime { get; set; }

    // ── Pay type resolved at clock-out / approval ─────────────────────────
    public DayPayType PayType { get; set; } = DayPayType.Regular;

    // ── Status ─────────────────────────────────────────────────────────────
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    /// <summary>Note supplied by agent on a retrospective submission.</summary>
    public string? AgentNote { get; set; }

    /// <summary>Reviewer comment when approving or rejecting a retrospective entry.</summary>
    public string? ReviewerNote { get; set; }

    /// <summary>UserId of the TL / Manager who reviewed a retrospective.</summary>
    public string? ReviewedById { get; set; }
    public AppUser? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    // ── Computed helpers (not mapped) ──────────────────────────────────────
    /// <summary>
    /// Raw worked minutes (CheckOut - CheckIn). Null if either timestamp is missing.
    /// </summary>
    public double? WorkedMinutes =>
        (CheckInTime.HasValue && CheckOutTime.HasValue)
            ? (CheckOutTime.Value - CheckInTime.Value).TotalMinutes
            : null;

    /// <summary>
    /// Billable hours after the 1-hour lunch deduction (minimum 0).
    /// Only meaningful on Regular / PublicHoliday / Vacation day types.
    /// </summary>
    public double? BillableHours =>
        WorkedMinutes.HasValue
            ? Math.Max(0, (WorkedMinutes.Value / 60.0) - 1.0)
            : null;
}
