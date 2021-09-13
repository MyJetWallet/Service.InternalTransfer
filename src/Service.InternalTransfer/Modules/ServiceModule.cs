using Autofac;
using MyJetWallet.Sdk.Grpc;
using MyJetWallet.Sdk.NoSql;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using MyServiceBus.Abstractions;
using Service.ChangeBalanceGateway.Client;
using Service.ClientWallets.Client;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Jobs;
using Service.InternalTransfer.Services;
using Service.VerificationCodes.Client;
using SimpleTrading.PersonalData.Abstractions.PersonalDataUpdate;
using SimpleTrading.PersonalData.Grpc;
using SimpleTrading.PersonalData.ServiceBus;

namespace Service.InternalTransfer.Modules
{
    public class ServiceModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var noSqlClient = builder.CreateNoSqlClient(Program.ReloadedSettings(e => e.MyNoSqlReaderHostPort));
            builder.RegisterSpotChangeBalanceGatewayClient(Program.Settings.ChangeBalanceGatewayGrpcServiceUrl);
            builder.RegisterClientWalletsClients(noSqlClient, Program.Settings.ClientWalletsGrpcServiceUrl);
            builder.RegisterVerificationCodesClient(Program.Settings.VerificationCodesGrpcUrl);
            
            var personalDataClientFactory = new MyGrpcClientFactory(Program.Settings.PersonalDataServiceUrl);
            builder
                .RegisterInstance(personalDataClientFactory.CreateGrpcService<IPersonalDataServiceGrpc>())
                .As<IPersonalDataServiceGrpc>()
                .SingleInstance();

            var spotServiceBusClient = builder.RegisterMyServiceBusTcpClient(Program.ReloadedSettings(e => e.SpotServiceBusHostPort), ApplicationEnvironment.HostName, Program.LogFactory);
            builder.RegisterMyServiceBusPublisher<Transfer>(spotServiceBusClient, Transfer.TopicName, false);

            var queueName = "Internal-Transfer-Service";
            builder.RegisterMyServiceBusSubscriberSingle<ITraderUpdate>(spotServiceBusClient, TopicNames.PersonalDataUpdate, queueName, TopicQueueType.Permanent);
            builder.RegisterMyServiceBusSubscriberSingle<TransferVerificationMessage>(spotServiceBusClient,
                TransferVerificationMessage.TopicName, queueName, TopicQueueType.Permanent);

            builder.RegisterType<TransferProcessingJob>().AsSelf().SingleInstance();
            builder.RegisterType<InternalTransferService>().AsSelf().SingleInstance();

        }
    }
}