using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Service;
using MyNoSqlServer.DataReader;
using MyServiceBus.TcpClient;
using Service.InternalTransfer.Jobs;

namespace Service.InternalTransfer
{
    public class ApplicationLifetimeManager : ApplicationLifetimeManagerBase
    {
        private readonly ILogger<ApplicationLifetimeManager> _logger;
        private readonly MyNoSqlTcpClient _myNoSqlTcpClient;
        private readonly MyServiceBusTcpClient _myServiceBusTcpClient;
        private readonly TransferProcessingJob _transferProcessingJob;
        public ApplicationLifetimeManager(IHostApplicationLifetime appLifetime, ILogger<ApplicationLifetimeManager> logger, MyNoSqlTcpClient myNoSqlTcpClient, MyServiceBusTcpClient myServiceBusTcpClient, TransferProcessingJob transferProcessingJob)
            : base(appLifetime)
        {
            _logger = logger;
            _myNoSqlTcpClient = myNoSqlTcpClient;
            _myServiceBusTcpClient = myServiceBusTcpClient;
            _transferProcessingJob = transferProcessingJob;
        }

        protected override void OnStarted()
        {
            _logger.LogInformation("OnStarted has been called");
            _myNoSqlTcpClient.Start();
            _logger.LogInformation("MyNoSqlTcpClient is started");
            _myServiceBusTcpClient.Start();
            _logger.LogInformation("MyServiceBusTcpClient is started");
            _transferProcessingJob.Start();
            _logger.LogInformation("TransferProcessingJob is started");
        }

        protected override void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called");
            _myNoSqlTcpClient.Stop();
            _logger.LogInformation("MyNoSqlTcpClient is stopped");
            _myServiceBusTcpClient.Stop();
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
