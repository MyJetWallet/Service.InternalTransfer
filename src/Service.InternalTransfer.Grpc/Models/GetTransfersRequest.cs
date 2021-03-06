using System;
using System.Runtime.Serialization;
using Service.InternalTransfer.Domain.Models;

namespace Service.InternalTransfer.Grpc.Models
{    
    [DataContract]
    public class GetTransfersRequest
    {
        [DataMember(Order = 1)] public long LastId { get; set; }
        [DataMember(Order = 2)] public int BatchSize { get; set; }
        [DataMember(Order = 3)] public string WalletId { get; set; }
        [DataMember(Order = 4)] public string AssetSymbol { get; set; }
        [DataMember(Order = 5)] public TransferStatus? WithdrawalStatus { get; set; }
        [DataMember(Order = 6)] public string ClientId { get; set; }
        [DataMember(Order = 7)] public string TransactionId { get; set; }
        [DataMember(Order = 8)] public DateTime? EventDateFrom { get; set; }
        [DataMember(Order = 9)] public DateTime? EventDateTo { get; set; }
    }
}