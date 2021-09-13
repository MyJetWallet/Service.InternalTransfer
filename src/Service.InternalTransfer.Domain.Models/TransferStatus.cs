namespace Service.InternalTransfer.Domain.Models
{
    public enum TransferStatus
    {
        New,
        ApprovalPending,
        Pending,
        WaitingForUser,
        Completed,
        Cancelled
    }
}