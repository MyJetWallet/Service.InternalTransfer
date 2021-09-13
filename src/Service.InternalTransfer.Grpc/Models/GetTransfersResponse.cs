using System.Collections.Generic;
using System.Runtime.Serialization;
using Service.InternalTransfer.Domain.Models;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class GetTransfersResponse
    {
        [DataMember(Order = 1)] public bool Success { get; set; }
        [DataMember(Order = 2)] public string ErrorMessage { get; set; }
        [DataMember(Order = 3)] public long IdForNextQuery { get; set; }
        [DataMember(Order = 4)] public List<Transfer> Transfers { get; set; }
    }
}