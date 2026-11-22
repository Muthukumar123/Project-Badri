using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Badri.TradeFlow;

// EF CLI only. Connection string defines SQL Server provider; migration generation
// does not open a connection. Do not use this design-time string at runtime.
public sealed class TradeDbDesignFactory : IDesignTimeDbContextFactory<TradeDb>
{
    public TradeDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TradeDb>()
            .UseSqlServer("Server=localhost;Database=BadriTradeFlowMigrationDesign;TrustServerCertificate=True;User ID=unused;Password=unused")
            .Options;
        return new TradeDb(options);
    }
}
