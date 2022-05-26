using JetBrains.Annotations;
using MyJetWallet.Sdk.Grpc;
using MyNoSqlServer.DataReader;
using Service.InternalTransfer.Domain.Models.NoSql;
using Service.InternalTransfer.Grpc;

namespace Service.InternalTransfer.Client
{
    [UsedImplicitly]
    public class InternalTransferClientFactory: MyGrpcClientFactory
    {
        
        private readonly MyNoSqlReadRepository<TransfersInProgressNoSqlEntity> _reader;

        public InternalTransferClientFactory(string grpcServiceUrl, MyNoSqlReadRepository<TransfersInProgressNoSqlEntity> reader) : base(grpcServiceUrl)
        {
            _reader = reader;
        }

        public ITransferByPhoneService GetTransferByPhoneService() => CreateGrpcService<ITransferByPhoneService>();
        
        public IInProgressTransfersService GetInProgressClient() => _reader != null
            ? new InProgressTransfersClient(CreateGrpcService<IInProgressTransfersService>(), _reader)
            : CreateGrpcService<IInProgressTransfersService>();
    }
}
