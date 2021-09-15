using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyJetWallet.Domain;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.Service.Tools;
using Newtonsoft.Json;
using Service.ClientWallets.Grpc;
using Service.ClientWallets.Grpc.Models;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Postgres;
using Service.InternalTransfer.Postgres.Models;
using Service.InternalTransfer.Services;
using Service.VerificationCodes.Grpc;
using Service.VerificationCodes.Grpc.Models;
using SimpleTrading.PersonalData.Abstractions.PersonalDataUpdate;
using SimpleTrading.PersonalData.Grpc;

namespace Service.InternalTransfer.Jobs
{
    public class TransferProcessingJob
    {
        private readonly ILogger<TransferProcessingJob> _logger;
        private readonly InternalTransferService _transferService;
        private readonly IPublisher<Transfer> _transferPublisher;
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;
        private readonly ITransferVerificationService _verificationService;
        private readonly IPersonalDataServiceGrpc _personalDataService;
        private readonly IClientWalletService _clientWalletService;
        private readonly MyTaskTimer _timer;
        
        public TransferProcessingJob(ILogger<TransferProcessingJob> logger, 
            InternalTransferService transferService, 
            IPublisher<Transfer> transferPublisher,
            ISubscriber<TransferVerificationMessage> verificationSubscriber, 
            DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder, IClientWalletService clientWalletService, 
            ISubscriber<ITraderUpdate> personalDataSubscriber, 
            IPersonalDataServiceGrpc personalDataService,
            ITransferVerificationService verificationService)
        {
            _logger = logger;
            _transferService = transferService;
            _transferPublisher = transferPublisher;
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _clientWalletService = clientWalletService;
            _personalDataService = personalDataService;
            _verificationService = verificationService;

            personalDataSubscriber.Subscribe(HandleTransfersToNewlyRegistered);
            verificationSubscriber.Subscribe(HandleApprovedTransfers);

            _timer = new MyTaskTimer(typeof(TransferProcessingJob),
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec),
                logger, DoTime);
        }
        
        private async Task DoTime()
        {
            await HandleExpiringTransfers();
            await HandleCancellingTransfers();
            await HandleNewTransfers();
            await HandlePendingTransfers();
        }

        private async ValueTask HandleApprovedTransfers(TransferVerificationMessage message)
        {
            using var activity = MyTelemetry.StartActivity("Handle approved transfers");
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var sw = new Stopwatch();
                sw.Start();

                var transfers = await context.Transfers.Where(e =>
                    e.Status == TransferStatus.ApprovalPending
                    && e.Id == long.Parse(message.TransferId)).ToListAsync();
                
                foreach (var transfer in transfers)
                    try
                    {
                        if (transfer.Cancelling)
                        {
                            transfer.Status = TransferStatus.Cancelled;
                            await PublishSuccess(transfer);
                            continue;
                        }

                        transfer.Status = TransferStatus.Pending;
                        
                        await PublishSuccess(transfer);
                    }
                    catch (Exception ex)
                    {
                        await HandleError(transfer, ex);
                    }

                await context.UpdateAsync(transfers);

                transfers.Count.AddToActivityAsTag("transfers-count");

                sw.Stop();
                if (transfers.Count > 0)
                    _logger.LogInformation("Handled {countTrade} approved transfers. Time: {timeRangeText}",
                        transfers.Count,
                        sw.Elapsed.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle approved transfers");
                ex.FailActivity();
                throw;
            }

            _timer.ChangeInterval(
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec));
        }
        
        
        private async Task HandleNewTransfers()
        {
            using var activity = MyTelemetry.StartActivity("Handle new transfers");
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var sw = new Stopwatch();
                sw.Start();

                var transfers = await context.Transfers.Where(e =>
                    e.Status == TransferStatus.New
                    && e.WorkflowState != WorkflowState.Failed).ToListAsync();


                var whitelist = new List<string>();
                var whitelistString = Program.ReloadedSettings(e => e.WhitelistedPhones).Invoke();
                if(!string.IsNullOrWhiteSpace(whitelistString))
                    whitelist = whitelistString.Split(';').ToList();

                foreach (var transfer in transfers)
                    try
                    {
                        if (transfer.Cancelling)
                        {
                            transfer.Status = TransferStatus.Cancelled;
                            await PublishSuccess(transfer);
                            continue;
                        }
                        
                        if (whitelist.Contains(transfer.DestinationPhoneNumber))
                        {
                            transfer.Status = TransferStatus.Pending;
                            await PublishSuccess(transfer);
                            continue;
                        }
                        
                        var response = await _verificationService.SendTransferVerificationCodeAsync(
                            new SendTransferVerificationCodeRequest()
                            {
                                ClientId = transfer.ClientId,
                                OperationId = transfer.Id.ToString(),
                                Lang = transfer.ClientLang,
                                AssetSymbol = transfer.AssetSymbol,
                                Amount = transfer.Amount.ToString(CultureInfo.InvariantCulture),
                                DestinationPhone = transfer.DestinationPhoneNumber,
                                IpAddress = transfer.ClientIp
                            });

                        if (!response.IsSuccess && !response.ErrorMessage.Contains("Cannot send again code"))
                            throw new Exception(
                                $"Failed to send verification email. Error message: {response.ErrorMessage}");

                        transfer.Status = TransferStatus.ApprovalPending;
                        transfer.NotificationTime = DateTime.UtcNow;
                        await PublishSuccess(transfer);
                    }
                    catch (Exception ex)
                    {
                        await HandleError(transfer, ex);
                    }

                await context.UpdateAsync(transfers);

                transfers.Count.AddToActivityAsTag("transfers-count");

                sw.Stop();
                if (transfers.Count > 0)
                    _logger.LogInformation("Handled {countTrade} new transfers. Time: {timeRangeText}",
                        transfers.Count,
                        sw.Elapsed.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle new transfers");
                ex.FailActivity();
                throw;
            }

            _timer.ChangeInterval(
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec));
        }

        private async Task HandlePendingTransfers()
        {
            using var activity = MyTelemetry.StartActivity("Handle pending transfers");
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var sw = new Stopwatch();
                sw.Start();

                var transfers = await context.Transfers.Where(e =>
                    e.Status == TransferStatus.Pending
                    && e.WorkflowState != WorkflowState.Failed).ToListAsync();
                
                foreach (var transfer in transfers)
                    try
                    {
                        if (transfer.Cancelling)
                        {
                            transfer.Status = TransferStatus.Cancelled;
                            await PublishSuccess(transfer);
                            continue;
                        }

                        if (string.IsNullOrEmpty(transfer.DestinationWalletId))
                            await _transferService.ExecuteTransferToServiceWallet(transfer);
                        else 
                            await _transferService.ExecuteTransfer(transfer);
                        
                        await PublishSuccess(transfer);
                    }
                    catch (Exception ex)
                    {
                        await HandleError(transfer, ex);
                    }

                await context.UpdateAsync(transfers);

                transfers.Count.AddToActivityAsTag("transfers-count");

                sw.Stop();
                if (transfers.Count > 0)
                    _logger.LogInformation("Handled {countTrade} pending transfers. Time: {timeRangeText}",
                        transfers.Count,
                        sw.Elapsed.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle pending transfers");
                ex.FailActivity();
                throw;
            }

            _timer.ChangeInterval(
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec));
        }
        
        private async Task HandleCancellingTransfers()
        {
            using var activity = MyTelemetry.StartActivity("Handle cancelling transfers");
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var sw = new Stopwatch();
                sw.Start();

                var transfers = await context.Transfers.Where(e =>
                    e.Status == TransferStatus.WaitingForUser
                    && e.WorkflowState != WorkflowState.Failed).ToListAsync();
                
                foreach (var transfer in transfers)
                    try
                    {
                        if (!transfer.Cancelling) 
                            continue;
                        
                        await _transferService.RefundTransfer(transfer);
                        await PublishSuccess(transfer);
                    }
                    catch (Exception ex)
                    {
                        await HandleError(transfer, ex);
                    }

                await context.UpdateAsync(transfers);

                transfers.Count.AddToActivityAsTag("transfers-count");

                sw.Stop();
                if (transfers.Count > 0)
                    _logger.LogInformation("Handled {countTrade} cancelling transfers. Time: {timeRangeText}",
                        transfers.Count,
                        sw.Elapsed.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle cancelling transfers");
                ex.FailActivity();
                throw;
            }

            _timer.ChangeInterval(
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec));
        }

        private async Task HandleExpiringTransfers()
        {
            using var activity = MyTelemetry.StartActivity("Handle expiring transfers");
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var sw = new Stopwatch();
                sw.Start();

                var transfers = await context.Transfers.Where(e =>
                    e.Status == TransferStatus.ApprovalPending
                    && e.WorkflowState != WorkflowState.Failed).ToListAsync();
                
                foreach (var transfer in transfers)
                    try
                    {
                        if (transfer.Cancelling)
                        {
                            transfer.Status = TransferStatus.Cancelled;
                            await PublishSuccess(transfer);
                            continue;
                        }
                        
                        if (DateTime.UtcNow - transfer.NotificationTime >=
                            TimeSpan.FromMinutes(Program.Settings.TransferExpirationTimeInMin))
                        {
                            transfer.Status = TransferStatus.Cancelled;
                            transfer.LastError = "Expired";
                            await PublishSuccess(transfer);
                        }
                    }
                    catch (Exception ex)
                    {
                        await HandleError(transfer, ex);
                    }

                await context.UpdateAsync(transfers);

                transfers.Count.AddToActivityAsTag("transfers-count");

                sw.Stop();
                if (transfers.Count > 0)
                    _logger.LogInformation("Handled {countTrade} expiring transfers. Time: {timeRangeText}",
                        transfers.Count,
                        sw.Elapsed.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle expiring transfers");
                ex.FailActivity();
                throw;
            }

            _timer.ChangeInterval(
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec));
        }
        
        private async ValueTask HandleTransfersToNewlyRegistered(ITraderUpdate traderUpdate)
        {
            using var activity = MyTelemetry.StartActivity($"Handle waiting for new users transfer for Client {traderUpdate.TraderId}");
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);
                var sw = new Stopwatch();
                sw.Start();
                
                var pd = await _personalDataService.GetByIdAsync(traderUpdate.TraderId);
                if(pd.PersonalData == null || string.IsNullOrEmpty(pd.PersonalData.Phone) || pd.PersonalData.ConfirmPhone == null)
                    return;

                var phone = pd.PersonalData.Phone;
                var clientId = pd.PersonalData.Id;

                var wallets =
                    await _clientWalletService.GetWalletsByClient(new JetClientIdentity("jetwallet",
                        pd.PersonalData.BrandId,
                        clientId));

                if (!wallets.Wallets.Any())
                {
                    _logger.LogError("No walletId found for client {clientId}", clientId);
                    throw new Exception($"No walletId found for client {clientId}");
                }
                
                var walletId = wallets.Wallets.First().WalletId;

                var transfers = await context.Transfers.Where(e =>
                    e.Status == TransferStatus.WaitingForUser
                    && e.DestinationPhoneNumber == phone
                    && e.WorkflowState != WorkflowState.Failed).ToListAsync();
                
                foreach (var transfer in transfers)
                    try
                    {
                        if (transfer.Cancelling)
                        {
                            await _transferService.RefundTransfer(transfer);
                            await PublishSuccess(transfer);
                            continue;
                        }
                        
                        transfer.DestinationClientId = clientId;
                        transfer.DestinationWalletId = walletId;
                        await _transferService.ExecuteTransferFromServiceWallet(transfer);
                        await PublishSuccess(transfer);
                    }
                    catch (Exception ex)
                    {
                        await HandleError(transfer, ex);
                    }

                await context.UpdateAsync(transfers);

                transfers.Count.AddToActivityAsTag("transfers-count");

                sw.Stop();
                if (transfers.Count > 0)
                    _logger.LogInformation("Handled {countTrade} waiting for new users  transfers. Time: {timeRangeText}",
                        transfers.Count,
                        sw.Elapsed.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot Handle waiting for new users  transfers");
                ex.FailActivity();
                throw;
            }

            _timer.ChangeInterval(
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec));
        }
        
        private async Task HandleError(TransferEntity transfer, Exception ex, bool retrying = true)
        {
            ex.FailActivity();
            
            transfer.WorkflowState =  WorkflowState.Retrying;
            
            transfer.LastError = ex.Message.Length > 2048 ? ex.Message.Substring(0, 2048) : ex.Message;
            transfer.RetriesCount++;
            if (transfer.RetriesCount >= Program.Settings.TransferRetriesLimit || !retrying)
            {
                transfer.WorkflowState = WorkflowState.Failed;
            }

            _logger.LogError(ex,
                "Transfer with Operation ID {operationId} changed workflow state to {status}. Operation: {operationJson}",
                transfer.TransactionId, transfer.WorkflowState, JsonConvert.SerializeObject(transfer));
            try
            {
                await _transferPublisher.PublishAsync(new Transfer(transfer));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Can not publish error operation status {operationJson}",
                    JsonConvert.SerializeObject(transfer));
            }
        }

        private async Task PublishSuccess(TransferEntity transfer)
        {
            var retriesCount = transfer.RetriesCount;
            var lastError = transfer.LastError;
            var state = transfer.WorkflowState;
            try
            {
                transfer.RetriesCount = 0;
                transfer.LastError = null;
                transfer.WorkflowState = WorkflowState.OK;

                await _transferPublisher.PublishAsync(new Transfer(transfer));
                _logger.LogInformation(
                    "Internal transfer with Operation ID {operationId} is changed to status {status}. Operation: {operationJson}",
                    transfer.TransactionId, transfer.Status, JsonConvert.SerializeObject(transfer));
            }
            catch
            {
                transfer.RetriesCount = retriesCount;
                transfer.LastError = lastError;
                transfer.WorkflowState = state;
                throw;
            }
        }

        private async Task CreateBufferWallet()
        {
            var bufferClient = new JetClientIdentity("jetwallet", "Monfex", Program.Settings.BufferClientId);
            var wallet = await _clientWalletService.GetWalletsByClient(bufferClient);
            if (!wallet.Wallets.Any())
            {
                var response = await _clientWalletService.CreateWalletAsync(new CreateWalletRequest
                {
                    ClientId = bufferClient,
                    Name = "Internal transfer buffer wallet",
                    BaseAsset = "BTC"
                });
                if (Program.Settings.BufferWalletId != response.WalletId)
                    throw new Exception();
            }
        }

        public void Start()
        {
            CreateBufferWallet();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }
    }
}