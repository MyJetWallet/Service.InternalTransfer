using System.Runtime.Serialization;
using Service.InternalTransfer.Domain.Models;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class InternalTransferResponse
    {
        [DataMember(Order = 1)]
        public string TransferId { get; set; }
        
        [DataMember(Order = 2)]
        public MEErrorCode ErrorCode { get; set; }
    }
}