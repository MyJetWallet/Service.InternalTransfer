using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Service;
using MyNoSqlServer.Abstractions;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Domain.Models.NoSql;
using Service.InternalTransfer.Grpc;
using Service.InternalTransfer.Grpc.Models;
using Service.InternalTransfer.Postgres;

namespace Service.InternalTransfer.Services
{
    public class InProgressTransfersService : IInProgressTransfersService
    {
        private readonly ILogger<InProgressTransfersService> _logger;
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;
        private readonly IMyNoSqlServerDataWriter<TransfersInProgressNoSqlEntity> _writer;

        private static readonly List<TransferStatus> InProgressStatuses = new()
            {TransferStatus.New, TransferStatus.WaitingForUser, TransferStatus.ApprovalPending, TransferStatus.WaitingForUser};

        public InProgressTransfersService(ILogger<InProgressTransfersService> logger, DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder, IMyNoSqlServerDataWriter<TransfersInProgressNoSqlEntity> writer)
        {
            _logger = logger;
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _writer = writer;
        }

        public async Task<InProgressResponse> GetInProgressTransfers(InProgressRequest request)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);
                var intentions = await context.Transfers.Where(t => t.AssetSymbol == request.Asset && t.ClientId == request.ClientId && InProgressStatuses.Contains(t.Status) ).ToListAsync();
                if (intentions.Any())
                {
                    var total = intentions.Sum(t => t.Amount);
                    var count = intentions.Count;

                    await _writer.InsertOrReplaceAsync(
                        TransfersInProgressNoSqlEntity.Create(request.ClientId, request.Asset, total, count));
                    
                    return new InProgressResponse
                    {
                        TotalAmount = total,
                        TxCount = count
                    };
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "When calculating in-progress buys for request {request}", request.ToJson());
            }
            
            return new InProgressResponse()
            {
                TotalAmount = 0,
                TxCount = 0
            };
        }
    }
}