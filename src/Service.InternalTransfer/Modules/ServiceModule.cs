﻿using Autofac;
using DotNetCoreDecorators;
using MyJetWallet.Sdk.Grpc;
using MyJetWallet.Sdk.NoSql;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using MyServiceBus.Abstractions;
using MyServiceBus.TcpClient;
using Service.ChangeBalanceGateway.Client;
using Service.ClientWallets.Client;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Jobs;
using Service.InternalTransfer.Services;
using Service.VerificationCodes.Client;
using SimpleTrading.PersonalData.Abstractions.PersonalDataUpdate;
using SimpleTrading.PersonalData.Grpc;
using SimpleTrading.PersonalData.ServiceBus;
using SimpleTrading.PersonalData.ServiceBus.PersonalDataUpdate;

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

            var queueName = "Spot-Internal-Transfer-Service";

            builder.RegisterMyServiceBusSubscriberSingle<TransferVerificationMessage>(spotServiceBusClient,
                TransferVerificationMessage.TopicName, queueName, TopicQueueType.Permanent);

            var serviceBusClient = MyServiceBusTcpClientFactory.Create(Program.ReloadedSettings(e => e.PersonalDataServiceBusHostPort), ApplicationEnvironment.HostName, Program.LogFactory.CreateLogger("PersonalDataServiceBus"));
            builder.RegisterInstance(serviceBusClient).SingleInstance();
            builder.RegisterInstance(new PersonalDataUpdateMyServiceBusSubscriber(serviceBusClient, queueName, TopicQueueType.Permanent, false))
                .As<ISubscriber<ITraderUpdate>>()
                .SingleInstance();
            
            builder.RegisterType<TransferProcessingJob>().AsSelf().SingleInstance();
            builder.RegisterType<InternalTransferService>().AsSelf().SingleInstance();

        }
        
    }
}