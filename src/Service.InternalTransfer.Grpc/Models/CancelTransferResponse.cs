using System.Runtime.Serialization;
using Service.InternalTransfer.Domain.Models;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class CancelTransferResponse
    {
        [DataMember(Order = 1)]
        public string TransferId { get; set; }
        
        [DataMember(Order = 2)]
        public bool IsSuccess { get; set; }
        
        [DataMember(Order = 3)]
        public string ErrorMessage { get; set; }
    }
}