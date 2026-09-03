# API reference

Every public member of both packages, generated from the XML documentation comments in the source. The same
comments ship inside the packages, so what is here is what IntelliSense shows at the call site.

`GenerateDocumentationFile` and `TreatWarningsAsErrors` are both on for every project under `src/`, so a public
member without a documentation comment does not compile in this repository — with one deliberate exception, below.

## Namespaces

| Namespace | Package | Contents |
|---|---|---|
| <xref:FmpDotNet> | `FmpDotNet` | <xref:FmpDotNet.FmpClient>, <xref:FmpDotNet.FmpOptions>, the <xref:FmpDotNet.FmpException> family, the two transports, <xref:FmpDotNet.FmpRequest>, the shared enums and the criteria records |
| <xref:FmpDotNet.Endpoints> | `FmpDotNet` | The 25 endpoint groups, one class each, reached through the properties of `FmpClient` |
| <xref:FmpDotNet.Models> | `FmpDotNet` | The response models — what each endpoint returns |
| <xref:FmpDotNet.Http> | `FmpDotNet` | The handlers and their bases, <xref:FmpDotNet.Http.TokenBucket>, <xref:FmpDotNet.Http.FmpBuckets> and <xref:FmpDotNet.Http.FmpBucketRegistry> |
| <xref:FmpDotNet.Serialization> | `FmpDotNet` | The `JsonConverter<T>` implementations and the CSV reader |
| <xref:FmpDotNet.Extensions.DependencyInjection> | `FmpDotNet.Extensions.DependencyInjection` | `AddFmp` in every form, the host-builder extensions, <xref:FmpDotNet.Extensions.DependencyInjection.IFmpBuilder>, <xref:FmpDotNet.Extensions.DependencyInjection.FmpClientFactory> |

## Two things no single member's page can say

**The converters are documented because they are reachable, not because they are an entry point.** The
`JsonSerializerContext` behind every model is `internal` and does not appear here; what is public in
<xref:FmpDotNet.Serialization> is the converters it registers. Read one when you want to know exactly which wire
spelling a value round-trips as — each says, and says what an unrecognised value does.

**Eight model types render their properties without summaries, by decision.** The seven period-shaped
fundamentals — <xref:FmpDotNet.Models.IncomeStatement>, <xref:FmpDotNet.Models.BalanceSheetStatement>,
<xref:FmpDotNet.Models.CashFlowStatement>, <xref:FmpDotNet.Models.KeyMetrics>,
<xref:FmpDotNet.Models.FinancialRatios>, <xref:FmpDotNet.Models.FinancialGrowth>,
<xref:FmpDotNet.Models.EnterpriseValues> — and <xref:FmpDotNet.Models.CotReport> are flat transcriptions of FMP's
wire fields, several hundred properties between them, and each file carries a `#pragma warning disable CS1591`
with the count and the reasoning at its top. Documenting each property individually would bury the type-level
remarks, which are where the real documentation is: what the endpoint actually does, and how it was measured to
do it.

The measured behaviour behind these types — plan gating, the two timezone conventions, what a `null` means — is in
the [README](../../README.md#upstream-behaviour-the-sdk-handles-for-you), rendered on this site as Reference, and the
guides link there rather than restating it.
