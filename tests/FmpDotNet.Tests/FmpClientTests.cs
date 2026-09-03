using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The core's own proof of the transport-pair pivot: a client is fully determined by two transports,
/// and disposing it disposes what it owns and nothing else.</summary>
public class FmpClientTests
{
    private static (FmpTransport Standard, FmpBulkTransport Bulk) Transports()
    {
        // No request is ever sent, so a bare HttpClient and unvalidated options are enough.
        var options = Options.Create(new FmpOptions { ApiKey = "k" });
        return (new FmpTransport(new HttpClient(), options), new FmpBulkTransport(new HttpClient(), options));
    }

    [Fact]
    public void Composes_every_group_from_the_transport_pair()
    {
        var (standard, bulk) = Transports();

        using var client = new FmpClient(standard, bulk);

        Assert.NotNull(client.Company);
        Assert.NotNull(client.Directory);
        Assert.NotNull(client.Statements);
        Assert.NotNull(client.Calendar);
        Assert.NotNull(client.Analyst);
        Assert.NotNull(client.Economics);
        Assert.NotNull(client.Search);
        Assert.NotNull(client.SecFilings);
        Assert.NotNull(client.InstitutionalOwnership);
        Assert.NotNull(client.InsiderTrades);
        Assert.NotNull(client.Congress);
        Assert.NotNull(client.Transcripts);
        Assert.NotNull(client.Esg);
        Assert.NotNull(client.Cot);
        Assert.NotNull(client.Quote);
        Assert.NotNull(client.Chart);
        Assert.NotNull(client.Bulk);
        Assert.NotNull(client.TechnicalIndicators);
        Assert.NotNull(client.MarketPerformance);
        Assert.NotNull(client.EtfAndFunds);
        Assert.NotNull(client.Indexes);
        Assert.NotNull(client.MarketHours);
        Assert.NotNull(client.News);
        Assert.NotNull(client.Fundraisers);
        Assert.NotNull(client.DiscountedCashFlow);
    }

    private sealed class Sentinel : IDisposable
    {
        public int Disposals;
        public void Dispose() => Disposals++;
    }

    [Fact]
    public void Dispose_disposes_what_it_owns_exactly_once()
    {
        var (standard, bulk) = Transports();
        var owned = new Sentinel();
        var client = new FmpClient(standard, bulk, owned);

        client.Dispose();
        client.Dispose();

        // The owner is a ServiceProvider in practice, whose Dispose is idempotent — but the client should not
        // rely on that, so it hands the owner over once and forgets it.
        Assert.Equal(1, owned.Disposals);
    }

    [Fact]
    public void Dispose_without_an_owner_is_a_no_op_and_the_client_stays_usable()
    {
        var (standard, bulk) = Transports();
        var client = new FmpClient(standard, bulk);

        client.Dispose();

        Assert.NotNull(client.Company);
    }
}
