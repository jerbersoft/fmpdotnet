using Microsoft.Extensions.Options;

namespace FinancialModelingPrep;

/// <summary>The transport for <c>*-bulk</c> endpoints.
///
/// <para>A distinct type only so dependency injection can hand it the bulk <c>HttpClient</c> — the one carrying the
/// small bulk token bucket and the long bulk timeout. Sharing one transport type would mean bulk downloads paced
/// against the ordinary 660/min reservoir, which FMP limits separately and far more tightly.</para></summary>
public sealed class FmpBulkTransport(HttpClient http, IOptions<FmpOptions> options) : FmpTransport(http, options);
