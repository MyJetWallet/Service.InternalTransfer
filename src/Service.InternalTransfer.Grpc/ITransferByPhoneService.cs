using System.ServiceModel;
using System.Threading.Tasks;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Grpc.Models;

namespace Service.InternalTransfer.Grpc
{
    [ServiceContract]
    public interface ITransferByPhoneService
    {
        [OperationContract]
        Task<InternalTransferResponse> TransferByPhone(TransferByPhoneRequest request);

        [OperationContract]
        Task<Transfer> GetTransferById(GetTransferByIdRequest request);

        [OperationContract]
        Task<CancelTransferResponse> CancelTransfer(CancelTransferRequest request);

        [OperationContract]
        Task<RetryTransferResponse> RetryWithdrawal(RetryTransferRequest request);

        [OperationContract]
        Task<GetTransfersResponse> GetTransfers(GetTransfersRequest request);

        [OperationContract]
        Task<ResendTransferVerificationResponse> ResendTransferConfirmationEmail(
            ResendTransferVerificationRequest request);
    }
}