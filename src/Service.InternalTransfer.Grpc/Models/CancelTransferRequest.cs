using System.Runtime.Serialization;

namespace Service.InternalTransfer.Grpc.Models
{ 
    [DataContract]
    public class CancelTransferRequest
    {
        [DataMember(Order = 1)]
        public string TransferId { get; set; }
    }
}