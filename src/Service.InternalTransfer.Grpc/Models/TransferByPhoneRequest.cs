using System.Runtime.Serialization;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class TransferByPhoneRequest
    {
        [DataMember(Order = 1)] public string RequestId { get; set; }
        [DataMember(Order = 2)] public string BrokerId { get; set; }
        [DataMember(Order = 3)] public string ClientId { get; set; }
        [DataMember(Order = 4)] public string WalletId { get; set; }
        [DataMember(Order = 5)] public string AssetSymbol { get; set; }
        [DataMember(Order = 6)] public double Amount { get; set; }
        [DataMember(Order = 7)] public string ToPhoneNumber { get; set; }
        [DataMember(Order = 8)] public string ClientLang { get; set; }
        [DataMember(Order = 9)] public string ClientIp { get; set; }
        [DataMember(Order = 10)] public string PhoneNumber { get; set; }
        [DataMember(Order = 11)] public string PhoneCode { get; set; }
        [DataMember(Order = 12)] public string PhoneIso { get; set; }
    }
}