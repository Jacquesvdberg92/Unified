namespace Unified.Models.Withdrawals;

public enum WithdrawalStatus
{
    Open,
    InProgress,
    WaitingOnClient,
    Done
}

public static class WithdrawalStatusExtensions
{
    public static string ToLabel(this WithdrawalStatus status) => status switch
    {
        WithdrawalStatus.Open            => "Open",
        WithdrawalStatus.InProgress      => "In progress",
        WithdrawalStatus.WaitingOnClient => "Waiting on client",
        WithdrawalStatus.Done            => "Done",
        _                                => status.ToString()
    };
}
