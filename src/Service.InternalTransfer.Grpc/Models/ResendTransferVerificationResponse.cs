using System.Runtime.Serialization;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class ResendTransferVerificationResponse
    {
        [DataMember(Order = 1)] public bool Success { get; set; }
        [DataMember(Order = 2)] public string ErrorMessage { get; set; }
        [DataMember(Order = 3)] public long Id { get; set; }
    }
}