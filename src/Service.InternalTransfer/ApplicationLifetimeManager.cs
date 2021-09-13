using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.NoSql;
using MyJetWallet.Sdk.Service;
using MyNoSqlServer.DataReader;
using MyServiceBus.TcpClient;
using Service.InternalTransfer.Jobs;

namespace Service.InternalTransfer
{
    public class ApplicationLifetimeManager : ApplicationLifetimeManagerBase
    {
        private readonly ILogger<ApplicationLifetimeManager> _logger;
        private readonly MyNoSqlClientLifeTime _myNoSqlClientLifeTime;
        private readonly MyServiceBusTcpClient[] _myServiceBusTcpClients;
        private readonly TransferProcessingJob _transferProcessingJob;
        public ApplicationLifetimeManager(IHostApplicationLifetime appLifetime, ILogger<ApplicationLifetimeManager> logger, MyServiceBusTcpClient[] myServiceBusTcpClients, TransferProcessingJob transferProcessingJob, MyNoSqlClientLifeTime myNoSqlClientLifeTime)
            : base(appLifetime)
        {
            _logger = logger;
            _myServiceBusTcpClients = myServiceBusTcpClients;
            _transferProcessingJob = transferProcessingJob;
            _myNoSqlClientLifeTime = myNoSqlClientLifeTime;
        }

        protected override void OnStarted()
        {
            _logger.LogInformation("OnStarted has been called");
            _myNoSqlClientLifeTime.Start();
            _logger.LogInformation("MyNoSqlTcpClient is started");
            foreach (var client in _myServiceBusTcpClients)
            {
                client.Start();
            }
            _logger.LogInformation("MyServiceBusTcpClient is started");
            _transferProcessingJob.Start();
            _logger.LogInformation("TransferProcessingJob is started");
        }

        protected override void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called");
            _myNoSqlClientLifeTime.Stop();
            _logger.LogInformation("MyNoSqlTcpClient is stopped");
            foreach (var client in _myServiceBusTcpClients)
            {
                client.Stop();
            }            
            _logger.LogInformation("MyServiceBusTcpClient is stopped");
            _transferProcessingJob.Stop();
            _logger.LogInformation("TransferProcessingJob is stopped");
        }

        protected override void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called.");
        }
    }
}
