using System.Runtime.Serialization;

namespace Service.InternalTransfer.Grpc.Models
{
    [DataContract]
    public class InProgressResponse 
    {
        [DataMember(Order = 1)]
        public decimal TotalAmount { get; set; }
        [DataMember(Order = 2)]
        public int TxCount { get; set; }
    }
}