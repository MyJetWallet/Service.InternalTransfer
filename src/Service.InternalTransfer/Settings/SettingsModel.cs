using MyJetWallet.Sdk.Service;
using MyYamlParser;

namespace Service.InternalTransfer.Settings
{
    public class SettingsModel
    {
        [YamlProperty("InternalTransfer.SeqServiceUrl")]
        public string SeqServiceUrl { get; set; }

        [YamlProperty("InternalTransfer.ZipkinUrl")]
        public string ZipkinUrl { get; set; }

        [YamlProperty("InternalTransfer.ElkLogs")]
        public LogElkSettings ElkLogs { get; set; }

        [YamlProperty("InternalTransfer.PostgresConnectionString")]
        public string PostgresConnectionString { get; set; }
        
        [YamlProperty("InternalTransfer.TransferProcessingIntervalSec")]
        public int TransferProcessingIntervalSec { get; set; }
        
        [YamlProperty("InternalTransfer.TransferExpirationTimeInMin")]
        public int TransferExpirationTimeInMin { get; set; }
        
        [YamlProperty("InternalTransfer.TransferRetriesLimit")]
        public int TransferRetriesLimit { get; set; }
        
        [YamlProperty("InternalTransfer.BufferClientId")]
        public string BufferClientId { get; set; }
        
        [YamlProperty("InternalTransfer.BufferWalletId")]
        public string BufferWalletId { get; set; }

        [YamlProperty("InternalTransfer.ClientWalletsGrpcServiceUrl")]
        public string ClientWalletsGrpcServiceUrl { get; set; }
        
        [YamlProperty("InternalTransfer.SpotServiceBusHostPort")]
        public string SpotServiceBusHostPort { get; set; }
        
        [YamlProperty("InternalTransfer.PersonalDataServiceUrl")]
        public string PersonalDataServiceUrl { get; set; }
        
        [YamlProperty("InternalTransfer.ChangeBalanceGatewayGrpcServiceUrl")]
        public string ChangeBalanceGatewayGrpcServiceUrl { get; set; }
        
        [YamlProperty("InternalTransfer.VerificationCodesGrpcUrl")]
        public string VerificationCodesGrpcUrl { get; set; }

        [YamlProperty("InternalTransfer.MyNoSqlReaderHostPort")]
        public string MyNoSqlReaderHostPort { get; set; }
        
        

    }
}
