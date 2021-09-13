namespace Service.InternalTransfer.Domain.Models
{
    public enum MEErrorCode
    {
        Ok,
        InternalError,
        LowBalance,
        AssetDoNotFound,
        AssetIsDisabled,
        WalletDoNotFound,
        InvalidAddress,
    }
}