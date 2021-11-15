using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyJetWallet.Sdk.Postgres;
using MyJetWallet.Sdk.Service;
using Service.InternalTransfer.Domain.Models;
using Service.InternalTransfer.Postgres.Models;

namespace Service.InternalTransfer.Postgres
{
public class DatabaseContext : MyDbContext
    {
        public const string Schema = "internal-transfers";

        private const string TransfersTableName = "internal-transfers";

        private Activity _activity;

        public DatabaseContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<TransferEntity> Transfers { get; set; }

        public static DatabaseContext Create(DbContextOptionsBuilder<DatabaseContext> options)
        {
            var activity = MyTelemetry.StartActivity($"Database context {Schema}")?.AddTag("db-schema", Schema);

            var ctx = new DatabaseContext(options.Options) {_activity = activity};

            return ctx;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            SetTransferEntry(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private void SetTransferEntry(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransferEntity>().ToTable(TransfersTableName);
            modelBuilder.Entity<TransferEntity>().Property(e => e.Id).UseIdentityColumn();
            modelBuilder.Entity<TransferEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<TransferEntity>().Property(e => e.BrokerId).HasMaxLength(128);
            modelBuilder.Entity<TransferEntity>().Property(e => e.ClientId).HasMaxLength(128);
            modelBuilder.Entity<TransferEntity>().Property(e => e.DestinationClientId).HasMaxLength(128).IsRequired(false);;
            modelBuilder.Entity<TransferEntity>().Property(e => e.DestinationWalletId).HasMaxLength(128).IsRequired(false);;
            modelBuilder.Entity<TransferEntity>().Property(e => e.WalletId).HasMaxLength(128);
            modelBuilder.Entity<TransferEntity>().Property(e => e.TransactionId).HasMaxLength(256);
            modelBuilder.Entity<TransferEntity>().Property(e => e.Amount);
            modelBuilder.Entity<TransferEntity>().Property(e => e.AssetSymbol).HasMaxLength(64);
            modelBuilder.Entity<TransferEntity>().Property(e => e.SenderPhoneNumber).HasMaxLength(64).IsRequired(false);;
            modelBuilder.Entity<TransferEntity>().Property(e => e.DestinationPhoneNumber).HasMaxLength(64);
            modelBuilder.Entity<TransferEntity>().Property(e => e.Status).HasDefaultValue(TransferStatus.New);
            modelBuilder.Entity<TransferEntity>().Property(e => e.MatchingEngineId).HasMaxLength(256).IsRequired(false);
            modelBuilder.Entity<TransferEntity>().Property(e => e.LastError).HasMaxLength(2048).IsRequired(false);
            modelBuilder.Entity<TransferEntity>().Property(e => e.RetriesCount).HasDefaultValue(0);
            modelBuilder.Entity<TransferEntity>().Property(e => e.EventDate);
            modelBuilder.Entity<TransferEntity>().Property(e => e.ClientIp).HasMaxLength(64).IsRequired(false);
            modelBuilder.Entity<TransferEntity>().Property(e => e.ClientLang).HasMaxLength(64).IsRequired(false);
            modelBuilder.Entity<TransferEntity>().Property(e => e.NotificationTime);
            modelBuilder.Entity<TransferEntity>().Property(e => e.RefundTransactionId).HasMaxLength(256).IsRequired(false);
            modelBuilder.Entity<TransferEntity>().Property(e => e.MeErrorCode).HasDefaultValue(MEErrorCode.Ok);
            modelBuilder.Entity<TransferEntity>().Property(e => e.WorkflowState).HasDefaultValue(WorkflowState.OK);
            modelBuilder.Entity<TransferEntity>().Property(e => e.SenderName).HasMaxLength(256).IsRequired(false);

            modelBuilder.Entity<TransferEntity>().HasIndex(e => e.Status);
            modelBuilder.Entity<TransferEntity>().HasIndex(e => e.TransactionId).IsUnique();

            modelBuilder.Entity<TransferEntity>().HasIndex(e => e.LastTs);
        }

        public async Task<int> InsertAsync(TransferEntity entity)
        {
            var result = await Transfers.Upsert(entity).On(e => e.TransactionId).NoUpdate().RunAsync();
            return result;
        }

        public async Task UpdateAsync(TransferEntity entity)
        {
            await UpdateAsync(new List<TransferEntity>{entity});
        }

        public async Task UpdateAsync(IEnumerable<TransferEntity> entities)
        {
            Transfers.UpdateRange(entities);
            await SaveChangesAsync();
        }
    }
}