using System;
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
using Service.PersonalData.Grpc;
using Service.PersonalData.Grpc.Contracts;

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
                string senderPhoneNumber = null;
                string senderName = null;
                var receiverIsRegistered = false;
                var client = await _personalDataService.GetByPhone(
                    new GetByPhoneRequest()
                    {
                        Phone = request.ToPhoneNumber
                    });
                if (client.PersonalData != null)
                {
                    destinationClient = client.PersonalData.Id;

                    var walletResponse = await _clientWalletService.GetWalletsByClient(new JetClientIdentity(request.BrokerId, client.PersonalData.BrandId, client.PersonalData.Id));
                    
                    if (!walletResponse.Wallets.Any())
                    {
                        _logger.LogError("No walletId found for client {clientId}", destinationClient);
                        return new InternalTransferResponse()
                        {
                            ErrorCode = MEErrorCode.WalletDoNotFound
                        };
                    }
                
                    destinationWallet = walletResponse.Wallets.First().WalletId;
                    receiverIsRegistered = true;
                }

                var sender = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = request.ClientId
                });
                if (sender.PersonalData != null)
                {
                    senderPhoneNumber = sender.PersonalData.Phone;
                    if(!string.IsNullOrWhiteSpace(sender.PersonalData.FirstName))
                        senderName = $"{sender.PersonalData.FirstName}";
                    
                    if(!string.IsNullOrWhiteSpace(sender.PersonalData.FirstName) && !string.IsNullOrWhiteSpace(sender.PersonalData.LastName))
                        senderName = $"{sender.PersonalData.FirstName} {sender.PersonalData.LastName[0]}.";
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
                    DestinationClientId = destinationClient,
                    SenderPhoneNumber = senderPhoneNumber,
                    SenderName = senderName
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
                    TransferId = withdrawalEntity.Id.ToString(),
                    ReceiverIsRegistered = receiverIsRegistered
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

        public async Task<GetTransfersResponse> GetTransfers(GetTransfersRequest request)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);
                var transfers = await context.Transfers
                    .Where(e => e.Id > request.LastId)
                    .OrderByDescending(e => e.Id)
                    .Take(request.BatchSize)
                    .ToListAsync();

                var response = new GetTransfersResponse
                {
                    Success = true,
                    Transfers = transfers.Select(e => new Transfer(e)).ToList(),
                    IdForNextQuery = transfers.Count > 0 ? transfers.Select(e => e.Id).Max() : 0
                };

                _logger.LogInformation("Return GetTransfers response count items: {count}",
                    response.Transfers.Count);
                return response;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Cannot get GetWithdrawals take: {takeValue}, LastId: {LastId}",
                    request.BatchSize, request.LastId);
                return new GetTransfersResponse {Success = false, ErrorMessage = exception.Message};
            }        
        }
    }
}
