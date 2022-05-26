using System.Threading.Tasks;
using MyNoSqlServer.Abstractions;
using Service.InternalTransfer.Domain.Models.NoSql;
using Service.InternalTransfer.Grpc;
using Service.InternalTransfer.Grpc.Models;

namespace Service.InternalTransfer.Client;

public class InProgressTransfersClient : IInProgressTransfersService
{
    private readonly IInProgressTransfersService _grpcService;
    private readonly IMyNoSqlServerDataReader<TransfersInProgressNoSqlEntity> _reader;

    public InProgressTransfersClient(IInProgressTransfersService grpcService, IMyNoSqlServerDataReader<TransfersInProgressNoSqlEntity> reader)
    {
        _grpcService = grpcService;
        _reader = reader;
    }

    public async Task<InProgressResponse> GetInProgressTransfers(InProgressRequest request)
    {
        var entity = _reader.Get(TransfersInProgressNoSqlEntity.GeneratePartitionKey(request.ClientId),
            TransfersInProgressNoSqlEntity.GenerateRowKey(request.Asset));
        if (entity != null)
            return new InProgressResponse
            {
                TotalAmount = entity.TotalAmount,
                TxCount = entity.Count
            };

        return await _grpcService.GetInProgressTransfers(request);
    }
}