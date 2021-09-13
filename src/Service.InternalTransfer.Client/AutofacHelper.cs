using Autofac;
using MyJetWallet.Sdk.ServiceBus;
using MyServiceBus.Abstractions;
using MyServiceBus.TcpClient;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Grpc;

// ReSharper disable UnusedMember.Global

namespace Service.InternalTransfer.Client
{
    public static class AutofacHelper
    {
        public static void RegisterInternalTransferClient(this ContainerBuilder builder, string grpcServiceUrl)
        {
            var factory = new InternalTransferClientFactory(grpcServiceUrl);

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
        }    }
}
