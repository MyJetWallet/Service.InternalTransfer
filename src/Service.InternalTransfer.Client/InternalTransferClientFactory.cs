using JetBrains.Annotations;
using MyJetWallet.Sdk.Grpc;
using Service.InternalTransfer.Grpc;

namespace Service.InternalTransfer.Client
{
    [UsedImplicitly]
    public class InternalTransferClientFactory: MyGrpcClientFactory
    {
        public InternalTransferClientFactory(string grpcServiceUrl) : base(grpcServiceUrl)
        {
        }

        public ITransferByPhoneService GetTransferByPhoneService() => CreateGrpcService<ITransferByPhoneService>();
    }
}
