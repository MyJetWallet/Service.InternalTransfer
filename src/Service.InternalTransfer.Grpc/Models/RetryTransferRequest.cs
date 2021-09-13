using System.Runtime.Serialization;

namespace Service.InternalTransfer.Grpc.Models
{ 
    [DataContract]
    public class RetryTransferRequest
    {
        [DataMember(Order = 1)]
        public string TransferId { get; set; }
    }
}