using System;
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
using SimpleTrading.PersonalData.Abstractions.PersonalData;
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
        //private readonly ISubscriber<TransferVerificationMessage> _verificationSubscriber;
        private readonly IPersonalDataServiceGrpc _personalDataService;
        private readonly IClientWalletService _clientWalletService;
        private readonly MyTaskTimer _timer;
        
        public TransferProcessingJob(ILogger<TransferProcessingJob> logger, 
            InternalTransferService transferService, 
            IPublisher<Transfer> transferPublisher,
            //ISubscriber<TransferVerificationMessage> verificationSubscriber, 
            DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder, IClientWalletService clientWalletService, 
            ISubscriber<ITraderUpdate> personalDataSubscriber, 
            IPersonalDataServiceGrpc personalDataService)
        {
            _logger = logger;
            _transferService = transferService;
            _transferPublisher = transferPublisher;
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _clientWalletService = clientWalletService;
            _personalDataService = personalDataService;

            personalDataSubscriber.Subscribe(HandleTransfersToNewlyRegistered);
            //_verificationSubscriber = verificationSubscriber;

            _timer = new MyTaskTimer(typeof(TransferProcessingJob),
                TimeSpan.FromSeconds(Program.Settings.TransferProcessingIntervalSec),
                logger, DoTime);
        }
        
        private async Task DoTime()
        {
            await HandleCancellingTransfers();
            await HandleNewTransfers();
            await HandlePendingTransfers();
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
                
                foreach (var transfer in transfers)
                    try
                    {
                        if (transfer.Cancelling)
                        {
                            transfer.Status = TransferStatus.Cancelled;
                            await PublishSuccess(transfer);
                            continue;
                        }
                        
                        
                        //WaitForVerification
                        
                        
                        transfer.Status = TransferStatus.Pending;
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

        private async ValueTask HandleTransfersToNewlyRegistered(ITraderUpdate traderUpdate)
        {
            using var activity = MyTelemetry.StartActivity("Handle waiting for new users transfers");
            try
            {
                var pd = await _personalDataService.GetByIdAsync(traderUpdate.TraderId);
                if(pd.PersonalData == null || string.IsNullOrEmpty(pd.PersonalData.Phone) || pd.PersonalData.ConfirmPhone == null)
                    return;

                var phone = pd.PersonalData.Phone;
                var clientId = pd.PersonalData.Id;
                
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var sw = new Stopwatch();
                sw.Start();

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