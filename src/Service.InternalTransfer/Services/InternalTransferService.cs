using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Service.ChangeBalanceGateway.Grpc;
using Service.ChangeBalanceGateway.Grpc.Models;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Postgres.Models;
using TransactionStatus = MyJetWallet.Domain.Transactions.TransactionStatus;

namespace Service.InternalTransfer.Services
{
    
    public class InternalTransferService
    {
        
        private readonly ISpotChangeBalanceService _changeBalanceService;
        private readonly ILogger<InternalTransferService> _logger;

        public InternalTransferService(ISpotChangeBalanceService changeBalanceService, ILogger<InternalTransferService> logger)
        {
            _changeBalanceService = changeBalanceService;
            _logger = logger;
        }

        public async Task ExecuteTransfer(Transfer transfer)
        {
            var responseCode = await SendTransferAsync(transfer.ClientId, transfer.WalletId,
                transfer.DestinationWalletId, transfer.TransactionId, transfer.Amount, transfer.AssetSymbol,
                transfer.BrokerId);
            if (responseCode != MEErrorCode.Ok)
            {
                _logger.LogError("When executing transfer {transfer}", JsonConvert.SerializeObject(transfer));
                throw new Exception(responseCode.ToString());
            }

            transfer.Status = TransferStatus.Completed;
        }

        public async Task ExecuteTransferToServiceWallet(Transfer transfer)
        {
            var responseCode = await SendTransferAsync(transfer.ClientId, transfer.WalletId,
                Program.Settings.BufferWalletId, transfer.TransactionId, transfer.Amount, transfer.AssetSymbol,
                transfer.BrokerId);
            if (responseCode != MEErrorCode.Ok)
            {
                _logger.LogError("When executing transfer to service wallet {transfer}",
                    JsonConvert.SerializeObject(transfer));
                throw new Exception(responseCode.ToString());
            }

            transfer.Status = TransferStatus.WaitingForUser;
        }

        public async Task ExecuteTransferFromServiceWallet(Transfer transfer)
        {
            var responseCode = await SendTransferAsync(Program.Settings.BufferClientId, Program.Settings.BufferWalletId,
                transfer.DestinationWalletId, transfer.TransactionId, transfer.Amount, transfer.AssetSymbol,
                transfer.BrokerId);
            if (responseCode != MEErrorCode.Ok)
            {
                _logger.LogError("When executing transfer from service wallet {transfer}",
                    JsonConvert.SerializeObject(transfer));
                throw new Exception(responseCode.ToString());
            }

            transfer.Status = TransferStatus.Completed;
        }

        public async Task RefundTransfer(Transfer transfer)
        {
            transfer.RefundTransactionId = OperationIdGenerator.GenerateOperationId("refund", transfer.TransactionId);
            var responseCode = await SendTransferAsync(Program.Settings.BufferClientId, Program.Settings.BufferWalletId,
                transfer.WalletId, transfer.RefundTransactionId, transfer.Amount, transfer.AssetSymbol,
                transfer.BrokerId);
            if (responseCode != MEErrorCode.Ok)
            {
                _logger.LogError("When refunding transfer {transfer}", JsonConvert.SerializeObject(transfer));
                throw new Exception(responseCode.ToString());
            }

            transfer.Status = TransferStatus.Cancelled;
        }


        private async Task<MEErrorCode> SendTransferAsync(string clientId, string fromWalletId, string toWalletId,
            string transactionId, double amount, string asset, string brokerId)
        {
            var request = new InternalTransferGrpcRequest
            {
                TransactionId = transactionId,
                ClientId = clientId,
                FromWalletId = fromWalletId,
                ToWalletId = toWalletId,
                Amount = amount,
                AssetSymbol = asset,
                BrokerId = brokerId,
                Integration = "TransferByPhone",
                Txid = string.Empty,
                Status = TransactionStatus.New
            };

            var changeBalanceResult = await _changeBalanceService.InternalTransferAsync(request);

            if (changeBalanceResult.ErrorCode == ChangeBalanceGrpcResponse.ErrorCodeEnum.WalletDoNotFound)
            {
                _logger.LogError("Got ME error code {errorCode}, request: {serializedRequest}",
                    changeBalanceResult.ErrorCode, JsonConvert.SerializeObject(request));
                return MEErrorCode.WalletDoNotFound;
            }

            if (changeBalanceResult.ErrorCode == ChangeBalanceGrpcResponse.ErrorCodeEnum.LowBalance)
            {
                _logger.LogError("Got ME error code {errorCode}, request: {serializedRequest}",
                    changeBalanceResult.ErrorCode, JsonConvert.SerializeObject(request));
                return MEErrorCode.LowBalance;
            }

            if (changeBalanceResult.ErrorCode != ChangeBalanceGrpcResponse.ErrorCodeEnum.Ok &&
                changeBalanceResult.ErrorCode != ChangeBalanceGrpcResponse.ErrorCodeEnum.Duplicate)
            {
                _logger.LogError("Got ME error code {errorCode}, request: {serializedRequest}",
                    changeBalanceResult.ErrorCode, JsonConvert.SerializeObject(request));
                return MEErrorCode.InternalError;
            }

            return MEErrorCode.Ok;
        }
        
        public Task RetryTransferAsync(TransferEntity transferEntity)
        {
            transferEntity.WorkflowState = WorkflowState.Retrying;
            _logger.LogInformation("Manual retry transfer with Operation Id {operationId} and status {status}", transferEntity.TransactionId, transferEntity.Status);
            return Task.CompletedTask;
        }
        
    }
}