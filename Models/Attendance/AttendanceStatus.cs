namespace Unified.Models.Attendance;

public enum AttendanceStatus
{
    /// <summary>Agent clocked in/out in real-time.</summary>
    Present,

    /// <summary>Agent submitted times retrospectively (missed punch).</summary>
    Retrospective,

    /// <summary>Retrospective entry approved by a Team Leader / Manager.</summary>
    Approved,

    /// <summary>Retrospective entry rejected by a Team Leader / Manager.</summary>
    Rejected
}

public enum DayPayType
{
    /// <summary>Regular weekday shift.</summary>
    Regular,

    /// <summary>Weekend shift — flat $80 USD.</summary>
    Weekend,

    /// <summary>Public holiday — double pay if worked.</summary>
    PublicHoliday,

    /// <summary>Vacation — paid at normal rate × scheduled hours.</summary>
    Vacation,

    /// <summary>Off day — unpaid.</summary>
    DayOff
}
