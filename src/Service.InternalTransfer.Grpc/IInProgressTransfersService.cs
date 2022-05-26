using System.ServiceModel;
using System.Threading.Tasks;
using Service.InternalTransfer.Grpc.Models;

namespace Service.InternalTransfer.Grpc
{
    [ServiceContract]
    public interface IInProgressTransfersService
    {
        [OperationContract]
        Task<InProgressResponse> GetInProgressTransfers(InProgressRequest request);
    }
}