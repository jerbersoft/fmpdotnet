using FmpDotNet.Endpoints;

namespace FmpDotNet;

/// <summary>Entry point to the FMP API, grouped the way FMP's own documentation groups it.
///
/// <para>Resolve this from dependency injection after calling
/// <see cref="DependencyInjection.FmpServiceCollectionExtensions.AddFmp(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{FmpOptions})"/>.</para></summary>
public sealed class FmpClient(CompanyEndpoints company, StatementEndpoints statements, BulkEndpoints bulk)
{
    /// <summary>Company profiles and identifiers.</summary>
    public CompanyEndpoints Company { get; } = company;

    /// <summary>The period-shaped fundamentals: statements, ratios, metrics, growth and enterprise values.</summary>
    public StatementEndpoints Statements { get; } = statements;

    /// <summary>Whole-universe CSV downloads. Streamed, and throttled separately — see
    /// <see cref="BulkEndpoints"/>.</summary>
    public BulkEndpoints Bulk { get; } = bulk;
}
