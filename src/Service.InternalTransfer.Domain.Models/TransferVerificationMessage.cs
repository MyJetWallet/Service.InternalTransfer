using System.Runtime.Serialization;

namespace Service.InternalTransfer.Domain.Models
{
    [DataContract]
    public class TransferVerificationMessage
    {
        public const string TopicName = "jet-wallet-transfer-phone-verification";
        
        [DataMember(Order = 1)] public string TransferId { get; set; }
        [DataMember(Order = 2)] public string ClientIp { get; set; }
    }
}