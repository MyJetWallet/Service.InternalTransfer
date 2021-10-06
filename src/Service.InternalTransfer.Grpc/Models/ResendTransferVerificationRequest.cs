using System.Runtime.Serialization;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class ResendTransferVerificationRequest
    {
        [DataMember(Order = 1)] public long Id { get; set; }
    }
}