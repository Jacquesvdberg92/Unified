namespace Unified.Models.Withdrawals;

/// <summary>Documents a client still owes before a withdrawal can be paid. Stored as a bit mask.</summary>
[Flags]
public enum WithdrawalDocuments
{
    None = 0,
    POI  = 1 << 0,
    POR  = 1 << 1,
    POD  = 1 << 2,
    CC   = 1 << 3,
    BD   = 1 << 4,
    CRP  = 1 << 5
}
