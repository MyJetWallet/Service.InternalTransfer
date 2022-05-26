using System;
using MyNoSqlServer.Abstractions;

namespace Service.InternalTransfer.Domain.Models.NoSql
{
    public class TransfersInProgressNoSqlEntity: MyNoSqlDbEntity
    {
        public const string TableName = "myjetwallet-internaltransfer-transfersinprogress";

        public static string GeneratePartitionKey(string clientId) => clientId;
        public static string GenerateRowKey(string assetId) => assetId;

        public decimal TotalAmount;
        public int Count;
        
        public static TransfersInProgressNoSqlEntity Create(string clientId, string assetId, decimal amount, int count)
        {
            return new TransfersInProgressNoSqlEntity()
            {
                PartitionKey = GeneratePartitionKey(clientId),
                RowKey = GenerateRowKey(assetId),
                TotalAmount = amount, 
                Count = count,
                Expires = DateTime.UtcNow.AddDays(1)
            };
        }
    }
}