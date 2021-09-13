﻿using System;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyJetWallet.Domain;
using MyJetWallet.Sdk.Service;
using Newtonsoft.Json;
using Service.ClientWallets.Grpc;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Grpc;
using Service.InternalTransfer.Grpc.Models;
using Service.InternalTransfer.Postgres;
using Service.InternalTransfer.Postgres.Models;
using Service.InternalTransfer.Settings;
using SimpleTrading.PersonalData.Abstractions.PersonalData;
using SimpleTrading.PersonalData.Grpc;

namespace Service.InternalTransfer.Services
{
    public class TransferByPhoneService: ITransferByPhoneService
    {
        private readonly ILogger<TransferByPhoneService> _logger;
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;
        private readonly IPersonalDataServiceGrpc _personalDataService;
        private readonly IClientWalletService _clientWalletService;
        private readonly IPublisher<Transfer> _transferPublisher;
        private readonly InternalTransferService _transferService;

        public TransferByPhoneService(ILogger<TransferByPhoneService> logger, DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder, IPersonalDataServiceGrpc personalDataService, IClientWalletService clientWalletService, IPublisher<Transfer> transferPublisher, InternalTransferService transferService)
        {
            _logger = logger;
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _personalDataService = personalDataService;
            _clientWalletService = clientWalletService;
            _transferPublisher = transferPublisher;
            _transferService = transferService;
        }

        public async Task<InternalTransferResponse> TransferByPhone(TransferByPhoneRequest request)
        {
            _logger.LogDebug("Receive CryptoWithdrawalRequest: {jsonText}", JsonConvert.SerializeObject(request));
            request.WalletId.AddToActivityAsTag("walletId");
            request.ClientId.AddToActivityAsTag("clientId");
            request.BrokerId.AddToActivityAsTag("brokerId");
            request.ToPhoneNumber.AddToActivityAsTag("phone-number");
            
            try
            {
                string destinationWallet = null;
                string destinationClient = null;

                var client = await _personalDataService.GetByPhone(request.ToPhoneNumber);
                if (client.PersonalData != null)
                {
                    destinationClient = client.PersonalData.Id;

                    var walletResponse = await _clientWalletService.GetWalletsByClient(
                        new JetClientIdentity(request.BrokerId, client.PersonalData.BrandId, client.PersonalData.Id));
                    
                    destinationWallet = walletResponse.Wallets.FirstOrDefault()?.WalletId;
                }

                var requestId = request.RequestId ?? Guid.NewGuid().ToString("N");
                var transactionId = OperationIdGenerator.GenerateOperationId(requestId, request.WalletId);

                await using var ctx = DatabaseContext.Create(_dbContextOptionsBuilder);
                var withdrawalEntity = new TransferEntity()
                {
                    BrokerId = request.BrokerId,
                    ClientId = request.ClientId,
                    WalletId = request.WalletId,
                    TransactionId = transactionId,
                    Amount = request.Amount,
                    AssetSymbol = request.AssetSymbol,
                    Status = TransferStatus.New,
                    EventDate = DateTime.UtcNow,
                    DestinationPhoneNumber = request.ToPhoneNumber,
                    ClientIp = request.ClientIp,
                    ClientLang = request.ClientLang,
                    DestinationWalletId = destinationWallet,
                    DestinationClientId = destinationClient
                };
                try
                {
                    await ctx.AddAsync(withdrawalEntity);
                    await ctx.SaveChangesAsync();
                }
                catch
                {
                    var existingWithdrawal = await ctx.Transfers.Where(t => t.TransactionId == transactionId).FirstAsync();
                    return new InternalTransferResponse()
                    {
                        TransferId = existingWithdrawal.Id.ToString()
                    };
                }

                return new InternalTransferResponse()
                {
                    TransferId = withdrawalEntity.Id.ToString()
                };
            }
            catch (Exception ex)
            {
                ex.FailActivity();
                _logger.LogError(ex, "Cannot handle CryptoWithdrawalRequest {jsonText}",
                    JsonConvert.SerializeObject(request));
                return new InternalTransferResponse()
                {
                    ErrorCode = MEErrorCode.InternalError
                };
            }
        }

        public async Task<Transfer> GetTransferById(GetTransferByIdRequest request)
        {
            await using var ctx = DatabaseContext.Create(_dbContextOptionsBuilder);
            var entity = await ctx.Transfers.Where(t=>t.Id == long.Parse(request.TransferId)).FirstOrDefaultAsync();
            return new Transfer(entity);
        }

        public async Task<CancelTransferResponse> CancelTransfer(CancelTransferRequest request)
        {
            try
            {
                await using var ctx = DatabaseContext.Create(_dbContextOptionsBuilder);
                var entity = await ctx.Transfers.Where(t => t.Id == long.Parse(request.TransferId))
                    .FirstOrDefaultAsync();
                if (entity == null)
                {
                    _logger.LogInformation("Unable to find transfer with id {transferId}", request.TransferId);
                    return new CancelTransferResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Unable to find withdrawal",
                        TransferId = request.TransferId
                    };
                }

                if (entity.Status == TransferStatus.Cancelled 
                    || entity.Status == TransferStatus.Completed)
                {
                    _logger.LogInformation("Incorrect status {status} for {transferId}", entity.Status,
                        request.TransferId);
                    return new CancelTransferResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Wrong status transfer {entity.Status}",
                        TransferId = request.TransferId
                    };
                }

                entity.LastError = "Manual cancel";
                entity.Cancelling = true;
                await ctx.SaveChangesAsync();
                await _transferPublisher.PublishAsync(new Transfer(entity));
                return new CancelTransferResponse()
                {
                    TransferId = request.TransferId,
                    IsSuccess = true
                };
            }
            catch (Exception e)
            {
                return new CancelTransferResponse()
                {
                    TransferId = request.TransferId,
                    ErrorMessage = e.Message,
                    IsSuccess = false
                };
            }
        }
        
        public async Task<RetryTransferResponse> RetryWithdrawal(RetryTransferRequest request)
        {
            try
            {
                await using var ctx = DatabaseContext.Create(_dbContextOptionsBuilder);
                var entity = await ctx.Transfers.Where(t => t.Id == long.Parse(request.TransferId))
                    .FirstOrDefaultAsync();
                if (entity == null)
                {
                    _logger.LogInformation("Unable to find transfer with id {transferId}", request.TransferId);
                    return new RetryTransferResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Unable to find withdrawal",
                        TransferId = request.TransferId
                    };
                }

                if (entity.Status == TransferStatus.Cancelled 
                    || entity.Status == TransferStatus.Completed)
                {
                    _logger.LogInformation("Incorrect status {status} for {transferId}", entity.Status,
                        request.TransferId);
                    return new RetryTransferResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Wrong status transfer {entity.Status}",
                        TransferId = request.TransferId
                    };
                }


                await _transferService.RetryTransferAsync(entity);
                await ctx.SaveChangesAsync();
                await _transferPublisher.PublishAsync(new Transfer(entity));
                return new RetryTransferResponse()
                {
                    TransferId = request.TransferId,
                    IsSuccess = true
                };
            }
            catch (Exception e)
            {
                return new RetryTransferResponse()
                {
                    TransferId = request.TransferId,
                    ErrorMessage = e.Message,
                    IsSuccess = false
                };
            }
        }
    }
}
