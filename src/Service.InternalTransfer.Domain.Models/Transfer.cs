using System;
using System.Runtime.Serialization;

namespace Service.InternalTransfer.Domain.Models
{
    [DataContract]
    public class Transfer
    {
        public Transfer(long id, string brokerId, string clientId, string walletId, string transactionId, double amount, string assetSymbol, string senderPhoneNumber, string destinationPhoneNumber, string destinationClientId, TransferStatus status, string matchingEngineId, string lastError, int retriesCount, DateTime eventDate, string clientLang, string clientIp, DateTime notificationTime, WorkflowState workflowState, string refundTransactionId, bool cancelling, MEErrorCode meErrorCode, string destinationWalletId)
        {
            Id = id;
            BrokerId = brokerId;
            ClientId = clientId;
            WalletId = walletId;
            TransactionId = transactionId;
            Amount = amount;
            AssetSymbol = assetSymbol;
            SenderPhoneNumber = senderPhoneNumber;
            DestinationPhoneNumber = destinationPhoneNumber;
            DestinationClientId = destinationClientId;
            DestinationWalletId = destinationWalletId;
            Status = status;
            MatchingEngineId = matchingEngineId;
            LastError = lastError;
            RetriesCount = retriesCount;
            EventDate = eventDate;
            ClientLang = clientLang;
            ClientIp = clientIp;
            NotificationTime = notificationTime;
            WorkflowState = workflowState;
            RefundTransactionId = refundTransactionId;
            Cancelling = cancelling;
            MeErrorCode = meErrorCode;
        }

        public Transfer(Transfer transfer) : this(transfer.Id, transfer.BrokerId, transfer.ClientId, transfer.WalletId, transfer.TransactionId, transfer.Amount, transfer.AssetSymbol, transfer.SenderPhoneNumber, transfer.DestinationPhoneNumber, transfer.DestinationClientId, transfer.Status, transfer.MatchingEngineId, transfer.LastError, transfer.RetriesCount, transfer.EventDate, transfer.ClientLang, transfer.ClientIp, transfer.NotificationTime, transfer.WorkflowState, transfer.RefundTransactionId, transfer.Cancelling, transfer.MeErrorCode, transfer.DestinationWalletId)
        {
            
        }
        
        public Transfer()
        {
        }

        public const string TopicName = "jet-wallet-transfer-phone-operation";

        [DataMember(Order = 1)] public long Id { get; set; }
        [DataMember(Order = 2)] public string BrokerId { get; set; }
        [DataMember(Order = 3)] public string ClientId { get; set; }
        [DataMember(Order = 4)] public string WalletId { get; set; }
        [DataMember(Order = 5)] public string TransactionId { get; set; }
        [DataMember(Order = 6)] public double Amount { get; set; }
        [DataMember(Order = 7)] public string AssetSymbol { get; set; }
        [DataMember(Order = 8)] public string SenderPhoneNumber { get; set; }
        [DataMember(Order = 9)] public string DestinationPhoneNumber { get; set; }
        [DataMember(Order = 10)] public string DestinationClientId { get; set; }
        [DataMember(Order = 11)] public string DestinationWalletId { get; set; }
        [DataMember(Order = 12)] public TransferStatus Status { get; set; }
        [DataMember(Order = 13)] public string MatchingEngineId { get; set; }
        [DataMember(Order = 14)] public string LastError { get; set; }
        [DataMember(Order = 15)] public int RetriesCount { get; set; }
        [DataMember(Order = 16)] public DateTime EventDate { get; set; }
        [DataMember(Order = 17)] public string ClientLang { get; set; }
        [DataMember(Order = 18)] public string ClientIp { get; set; }
        [DataMember(Order = 19)] public DateTime NotificationTime { get; set; }
        [DataMember(Order = 20)] public WorkflowState WorkflowState { get; set; }
        [DataMember(Order = 21)] public string RefundTransactionId { get; set; }
        [DataMember(Order = 22)] public bool Cancelling { get; set; }
        [DataMember(Order = 23)] public  MEErrorCode MeErrorCode { get; set; }
        [DataMember(Order = 24)] public string SenderName { get; set; }
    }
}