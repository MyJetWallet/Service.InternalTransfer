using Autofac;
using MyJetWallet.Sdk.ServiceBus;
using MyNoSqlServer.Abstractions;
using MyNoSqlServer.DataReader;
using MyServiceBus.Abstractions;
using MyServiceBus.TcpClient;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Domain.Models.NoSql;
using Service.InternalTransfer.Grpc;

// ReSharper disable UnusedMember.Global

namespace Service.InternalTransfer.Client
{
    public static class AutofacHelper
    {
        public static void RegisterInternalTransferClient(this ContainerBuilder builder, string grpcServiceUrl)
        {
            var factory = new InternalTransferClientFactory(grpcServiceUrl, null);

            builder.RegisterInstance(factory.GetTransferByPhoneService()).As<ITransferByPhoneService>().SingleInstance();
        }
        
        public static void RegisterTransferVerificationPublisher(this ContainerBuilder builder, MyServiceBusTcpClient serviceBusClient)
        {
            builder.RegisterMyServiceBusPublisher<TransferVerificationMessage>(serviceBusClient, TransferVerificationMessage.TopicName, true);
        }
        
        public static void RegisterTransferOperationSubscriber(this ContainerBuilder builder, MyServiceBusTcpClient serviceBusClient, string queue)
        {
            builder.RegisterMyServiceBusSubscriberBatch<Transfer>(serviceBusClient, Transfer.TopicName, queue,
                TopicQueueType.Permanent);
        }
        
        public static void RegisterTransferInProgressClient(this ContainerBuilder builder, string grpcServiceUrl, IMyNoSqlSubscriber myNoSqlSubscriber)
        {
            var subs = new MyNoSqlReadRepository<TransfersInProgressNoSqlEntity>(myNoSqlSubscriber, TransfersInProgressNoSqlEntity.TableName);

            var factory = new InternalTransferClientFactory(grpcServiceUrl, subs);

            builder.RegisterInstance(factory.GetInProgressClient()).As<IInProgressTransfersService>().SingleInstance();
            
            builder
                .RegisterInstance(subs)
                .As<IMyNoSqlServerDataReader<TransfersInProgressNoSqlEntity>>()
                .SingleInstance();

            builder
                .RegisterInstance(factory.GetInProgressClient())
                .As<IInProgressTransfersService>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}
