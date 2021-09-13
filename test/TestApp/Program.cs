using System;
using System.Threading.Tasks;
using ProtoBuf.Grpc.Client;
using Service.InternalTransfer.Client;
using Service.InternalTransfer.Grpc.Models;

namespace TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;

            Console.Write("Press enter to start");
            Console.ReadLine();


            var factory = new InternalTransferClientFactory("http://localhost:5001");
            var client = factory.GetTransferByPhoneService();
            //
            // var resp = await  client.TransferByPhone(new TransferByPhoneRequest(){Name = "Alex"});
            // Console.WriteLine(resp?.Message);

            Console.WriteLine("End");
            Console.ReadLine();
        }
    }
}
