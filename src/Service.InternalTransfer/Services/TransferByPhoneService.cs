using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyJetWallet.Domain;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using Newtonsoft.Json;
using Service.ClientWallets.Grpc;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Grpc;
using Service.InternalTransfer.Grpc.Models;
using Service.InternalTransfer.Postgres;
using Service.InternalTransfer.Postgres.Models;
using Service.PersonalData.Grpc;
using Service.PersonalData.Grpc.Contracts;
using Service.VerificationCodes.Grpc;
using Service.VerificationCodes.Grpc.Models;

namespace Service.InternalTransfer.Services
{
    public class TransferByPhoneService: ITransferByPhoneService
    {
        private readonly ILogger<TransferByPhoneService> _logger;
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;
        private readonly IPersonalDataServiceGrpc _personalDataService;
        private readonly IClientWalletService _clientWalletService;
        private readonly IServiceBusPublisher<Transfer> _transferPublisher;
        private readonly InternalTransferService _transferService;
        private readonly ITransferVerificationService _verificationService;

        public TransferByPhoneService(ILogger<TransferByPhoneService> logger, DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder, IPersonalDataServiceGrpc personalDataService, IClientWalletService clientWalletService, IServiceBusPublisher<Transfer> transferPublisher, InternalTransferService transferService, ITransferVerificationService verificationService)
        {
            _logger = logger;
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _personalDataService = personalDataService;
            _clientWalletService = clientWalletService;
            _transferPublisher = transferPublisher;
            _transferService = transferService;
            _verificationService = verificationService;
        }

        public async Task<InternalTransferResponse> TransferByPhone(TransferByPhoneRequest request)
        {
            _logger.LogDebug("Receive TransferByPhoneRequest: {jsonText}", JsonConvert.SerializeObject(request));
            request.WalletId.AddToActivityAsTag("walletId");
            request.ClientId.AddToActivityAsTag("clientId");
            request.BrokerId.AddToActivityAsTag("brokerId");
            request.ToPhoneNumber.AddToActivityAsTag("phone-number");
            
            try
            {
                var phoneNumber = string.IsNullOrWhiteSpace(request.ToPhoneNumber)
                    ? $"{request.PhoneCode}{request.PhoneNumber}" 
                    : request.ToPhoneNumber;
                
                string destinationWallet = null;
                string destinationClient = null;
                string senderPhoneNumber = null;
                string senderName = null;
                var receiverIsRegistered = false;
                var clients = await _personalDataService.GetByPhoneList(new GetByPhoneRequest()
                {
                    Phone = phoneNumber
                });
                
                if (clients.PersonalDatas?.Count() > 1)
                {
                    _logger.LogError("More than one client found for phone number {phone}", phoneNumber);
                    return new InternalTransferResponse()
                    {
                        ErrorCode = MEErrorCode.InvalidPhone
                    };
                }

                var client = clients.PersonalDatas?.SingleOrDefault();
                if (client != null)
                {
                    destinationClient = client.Id;

                    var walletResponse = await _clientWalletService.GetWalletsByClient(new JetClientIdentity(request.BrokerId, client.BrandId, client.Id));
                    
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
                    DestinationPhoneNumber = phoneNumber,
                    ClientIp = request.ClientIp,
                    ClientLang = request.ClientLang,
                    DestinationWalletId = destinationWallet,
                    DestinationClientId = destinationClient,
                    SenderPhoneNumber = senderPhoneNumber,
                    SenderName = senderName,
                    PhoneModel = request.PhoneModel,
                    Location = request.Location
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
            request.AddToActivityAsJsonTag("request-data");
            _logger.LogInformation("Receive GetTransfers request: {JsonRequest}", JsonConvert.SerializeObject(request));
            
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);
                var query = context.Transfers.AsNoTracking();
                
                if (request.LastId > 0)
                {
                    query = query.Where(e => e.Id < request.LastId);
                }
                
                if (!string.IsNullOrWhiteSpace(request.WalletId))
                {
                    query = query.Where(e => e.WalletId == request.WalletId);
                }
                
                if (!string.IsNullOrWhiteSpace(request.TransactionId))
                {
                    query = query.Where(e => e.TransactionId == request.TransactionId);
                }
                
                if (!string.IsNullOrWhiteSpace(request.ClientId))
                {
                    query = query.Where(e => e.ClientId == request.ClientId);
                }
                
                if (request.EventDateFrom != null)
                {
                    query = query.Where(e => e.EventDate >= request.EventDateFrom);
                }
                
                if (request.EventDateTo != null)
                {
                    query = query.Where(e => e.EventDate <= request.EventDateTo);
                }
                
                if (!string.IsNullOrWhiteSpace(request.AssetSymbol))
                {
                    query = query.Where(e => e.AssetSymbol == request.AssetSymbol);
                }

                if (request.WithdrawalStatus != null)
                {
                    query = query.Where(e => e.Status == request.WithdrawalStatus);
                }

                var transfers = await query
                    .OrderByDescending(e => e.Id)
                    .Take(request.BatchSize)
                    .ToListAsync();

                var response = new GetTransfersResponse
                {
                    Success = true,
                    Transfers = transfers.Select(e => new Transfer(e)).ToList(),
                    IdForNextQuery = transfers.Count > 0 ? transfers.Min(d => d.Id) : 0
                };

                response.Transfers.Count.AddToActivityAsTag("response-count-items");
                _logger.LogInformation("Return GetTransfers response count items: {count}",
                    response.Transfers.Count);
                return response;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Cannot get GetTransfers take: {takeValue}, LastId: {LastId}",
                    request.BatchSize, request.LastId);
                return new GetTransfersResponse {Success = false, ErrorMessage = exception.Message};
            }    
        }
        
        public async Task<ResendTransferVerificationResponse> ResendTransferConfirmationEmail(ResendTransferVerificationRequest request)
        {
            using var activity = MyTelemetry.StartActivity("Handle transfer verification resend")
                .AddTag("TransferId", request.Id);
            _logger.LogInformation("Handle transfer verification resend: {transferId}", request.Id);
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var transferEntity = await context.Transfers.FindAsync(request.Id);

                if (transferEntity == null)
                {
                    _logger.LogInformation("Unable to find transfer with id {transferId}", request.Id);
                    return new ResendTransferVerificationResponse
                    {
                        Success = false,
                        ErrorMessage = "Unable to find transfer",
                        Id = request.Id
                    };
                }

                if (transferEntity.Status != TransferStatus.ApprovalPending)
                {
                    return new ResendTransferVerificationResponse
                    {
                        Success = true,
                        Id = request.Id
                    };
                }

                var response = await _verificationService.SendTransferVerificationCodeAsync(
                    new SendTransferVerificationCodeRequest()
                    {
                        ClientId = transferEntity.ClientId,
                        OperationId = transferEntity.Id.ToString(),
                        Lang = transferEntity.ClientLang,
                        AssetSymbol = transferEntity.AssetSymbol,
                        Amount = transferEntity.Amount.ToString(CultureInfo.InvariantCulture),
                        DestinationPhone = transferEntity.DestinationPhoneNumber,
                        IpAddress = transferEntity.ClientIp,
                        PhoneModel = transferEntity.PhoneModel,
                        Timestamp = transferEntity.EventDate.ToString("F"),
                        Location = transferEntity.Location
                    });

                if (!response.IsSuccess)
                {
                    if (response.ErrorMessage.Contains("Cannot send again code"))
                    {
                        return new ResendTransferVerificationResponse
                        {
                            Success = true,
                            Id = request.Id
                        };
                    }
                    
                    return new ResendTransferVerificationResponse
                    {
                        Success = false,
                        ErrorMessage = response.ErrorMessage,
                        Id = request.Id
                    };
                }
                
                transferEntity.NotificationTime = DateTime.UtcNow;
                await context.UpdateAsync(new List<TransferEntity> {transferEntity});

                _logger.LogInformation("Handled transfer verification resend: {transferId}", request.Id);
                return new ResendTransferVerificationResponse
                {
                    Success = true,
                    Id = request.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle transfer verification resend");
                ex.FailActivity();

                return new ResendTransferVerificationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error {ex.Message}",
                    Id = request.Id
                };
            }
            
        }
    }
}
