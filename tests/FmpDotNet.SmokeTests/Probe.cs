using System.Collections;
using System.Reflection;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.SmokeTests;

/// <summary>What one endpoint answered when it was called for real.</summary>
/// <param name="Group">The <see cref="FmpClient"/> property the method hangs off, e.g. <c>Statements</c>.</param>
/// <param name="Method">The method's name.</param>
/// <param name="Outcome">One of the <see cref="Probe"/> outcome constants.</param>
/// <param name="Detail">Row count, or the exception text — for the failure message only. Never recorded in a
/// baseline: an exception message carries dates and a row count changes daily, so recording either would make
/// every run drift against the last.
///
/// <para>This reaches a CI log verbatim, so it must not carry the key, and it does not: FMP authenticates by
/// query string, but <see cref="FmpRequest.ToString"/> renders without the key and
/// <see cref="Http.UriRedaction"/> strips it from the one place a handler has only the built URI. That is a
/// property of the SDK rather than of this suite — which is the reason a failure here can print what the
/// upstream actually said instead of a sanitised summary of it.</para></param>
/// <param name="Set">Properties populated on at least one returned row.</param>
/// <param name="Unset">Properties null, empty or blank on every returned row.</param>
/// <param name="Rows">How many rows came back, or 0 for any outcome other than <see cref="Probe.Rows"/>. Not
/// written to a baseline — a row count changes daily and recording one would make every run drift against the
/// last. It is here for the one assertion that has to be about volume rather than shape: see
/// <c>OrdinaryEndpointShapeTests.The_classification_universe_still_comes_back_whole</c>.</param>
public sealed record Observation(
    string Group, string Method, string Outcome, string? Detail,
    IReadOnlyList<string> Set, IReadOnlyList<string> Unset, int Rows = 0)
{
    /// <summary>How this endpoint is named in a baseline file and in a failure message.</summary>
    public string Name => $"{Group}.{Method}";
}

/// <summary>Calls every modelled endpoint once and records the shape of what came back.
///
/// <para><b>Why population and not just "it returned something".</b> Nearly every property on nearly every model
/// in this SDK is nullable and not <c>required</c> — <c>CompanyProfile</c> declares 36 JSON properties and none of
/// them are required, and the fundamentals models run to 66. <c>System.Text.Json</c> answers a field it cannot
/// find with <see langword="null"/> and no error, so if FMP renamed <c>netIncome</c> tomorrow the SDK would keep
/// returning the same number of rows, of the same type, deserialised successfully, with that one value silently
/// gone. A smoke test that asserted "a non-empty list of IncomeStatement came back" would pass on the day the
/// data stopped arriving. Recording <b>which properties actually carry a value</b> is what makes a rename
/// visible, and it is the reason this suite exists at all.</para>
///
/// <para>The sweep is driven by reflection over <see cref="FmpClient"/> rather than by a hand-written list of
/// calls, so an endpoint added later is probed without anyone remembering to add it here. The price is that
/// arguments have to be synthesised, which <see cref="Argument"/> does by parameter name — and throws rather than
/// defaulting when it meets one it does not know, so a new parameter fails loudly instead of quietly sending a
/// zero.</para></summary>
internal static class Probe
{
    /// <summary>FMP answered, with at least one row.</summary>
    public const string Rows = "rows";

    /// <summary>FMP answered successfully with nothing in it. A legitimate answer for some endpoints and a
    /// symptom for others, which is why it is a recorded outcome rather than a failure.</summary>
    public const string Empty = "empty";

    /// <summary>FMP refused: 402, an entitlement answer about the endpoint.</summary>
    public const string PlanRequired = "plan-402";

    /// <summary>FMP refused: 403, which points at the key at least as often as at the plan.</summary>
    public const string Forbidden = "plan-403";

    /// <summary>Anything else — an error envelope, a bad status, a converter that could not read a value.</summary>
    public const string Error = "error";

    /// <summary>How many rows of a streamed CSV endpoint are read before the download is abandoned.
    ///
    /// <para>Enough rows that a genuinely sparse column is distinguishable from a column that stopped arriving —
    /// one row of a whole-universe bulk file would record half the model as null and detect nothing. Small enough
    /// that the response is aborted well inside a file measured in tens of megabytes: breaking out of the
    /// enumeration disposes the response, which closes the chunked stream mid-transfer.</para>
    ///
    /// <para><b>25 is what the bulk tier can afford, and the number was arrived at the hard way.</b> Measured on
    /// the same twenty bulk endpoints: 25 rows takes <b>8 m 4 s</b>, 200 rows takes <b>2 h 39 m</b> and two
    /// endpoints failed outright. Eight times the rows cost twenty times the wall clock, so most of that is not
    /// row-reading — but whatever it is, 200 does not fit inside a scheduled job, and a smoke suite nobody can
    /// afford to run is not a smoke suite. The ordinary tier is unaffected: 49 endpoints in 13 seconds, measured
    /// 2026-08-27 — roughly 20 MB of it whole-universe quote downloads, which cost bytes rather than throttle.</para>
    ///
    /// <para><b>The known cost of 25, left standing deliberately.</b> The 2026-08-26 sweep recorded
    /// <c>BulkCompanyProfile.Cik</c> as absent from <c>profile-bulk</c>, while the fixture captured from that
    /// same endpoint hours earlier carries a CIK on two of its first three rows and the unit test asserting one
    /// passes. Nothing was broken: a part is an unordered shard FMP republishes every few hours, and 25
    /// consecutive rows had landed in a run of listings without one. So a bulk <c>null</c> line can flip to
    /// <c>set</c> on a routine refresh and be reported as drift.</para>
    ///
    /// <para><b>Raising the sample is the wrong fix for that, which is the useful thing the 200-row run
    /// bought.</b> No sample size anyone can afford makes a reshuffled shard stable — <c>null</c> in a bulk
    /// baseline can only ever mean "null across the first N rows of one shard on one day", which is not a fact
    /// about the API. The fix, when it is worth making, is to stop recording <c>null</c> for the bulk tier and
    /// compare only what was populated. That is a change to what the baseline claims, so it wants its own
    /// measurement rather than being smuggled in here. Until then the file header says what a bulk
    /// <c>null</c> means, and a flip costs one regeneration.</para></summary>
    private const int StreamSample = 25;

    /// <summary>Calls every endpoint on the bulk groups, or every endpoint on the ordinary ones.</summary>
    public static async Task<IReadOnlyList<Observation>> SweepAsync(bool bulk)
    {
        var client = LiveApi.Client;
        var observations = new List<Observation>();

        foreach (var property in Groups())
        {
            if (IsBulk(property.PropertyType) != bulk) continue;
            var endpoints = property.GetValue(client)!;
            foreach (var method in EndpointMethods(property.PropertyType))
                observations.Add(await ObserveAsync(property.Name, method, endpoints).ConfigureAwait(false));
        }

        return observations;
    }

    /// <summary>The client's endpoint groups. Same reflection the README coverage test uses, for the same
    /// reason: a group added to <see cref="FmpClient"/> is swept without being registered anywhere.</summary>
    public static IEnumerable<PropertyInfo> Groups() =>
        typeof(FmpClient).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>Every public endpoint method on a group.</summary>
    public static IEnumerable<MethodInfo> EndpointMethods(Type group) =>
        group.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal);

    /// <summary>Whether a group talks to the separately-throttled bulk client.
    ///
    /// <para>Decided by the transport the group is constructed with, not by its name. A group called
    /// <c>Bulk</c> is what it is because it takes an <see cref="FmpBulkTransport"/>; matching on the string
    /// would put a future <c>BulkEtfEndpoints</c> in the wrong tier — or, worse, put an ordinary group in the
    /// bulk tier and leave it unprobed by the scheduled run.</para></summary>
    public static bool IsBulk(Type group) =>
        group.GetConstructors().Single().GetParameters()[0].ParameterType == typeof(FmpBulkTransport);

    private static async Task<Observation> ObserveAsync(string group, MethodInfo method, object endpoints)
    {
        IReadOnlyList<object> rows;
        try
        {
            rows = await InvokeAsync(method, endpoints).ConfigureAwait(false);
        }
        catch (FmpPlanRestrictedException ex)
        {
            return new Observation(group, method.Name, $"plan-{(int)ex.StatusCode}", ex.Message, [], []);
        }
        catch (Exception ex)
        {
            return new Observation(group, method.Name, Error, $"{ex.GetType().Name}: {ex.Message}", [], []);
        }

        // Properties are recorded only when there are rows to read them from. An empty answer would otherwise
        // record every property as absent, and the next run — with rows — would report the whole model as drift.
        if (rows.Count == 0)
            return new Observation(group, method.Name, Empty, "0 rows", [], []);

        var set = new List<string>();
        var unset = new List<string>();
        foreach (var property in Fields(ElementType(method.ReturnType)))
            (rows.Any(row => Populated(property.GetValue(row))) ? set : unset).Add(property.Name);

        return new Observation(group, method.Name, Rows, $"{rows.Count} rows", set, unset, rows.Count);
    }

    /// <summary>Calls the method and materialises whatever it answers into a flat list of rows.</summary>
    private static async Task<IReadOnlyList<object>> InvokeAsync(MethodInfo method, object endpoints)
    {
        object? result;
        try
        {
            result = method.Invoke(endpoints, [.. method.GetParameters().Select(Argument)]);
        }
        catch (TargetInvocationException ex)
        {
            // Reflection wraps everything the method threw. Rethrowing the inner exception is what lets the
            // catch clauses above see an FmpPlanRestrictedException as itself rather than as a wrapper.
            throw ex.InnerException ?? ex;
        }

        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                return Flatten(Result(task));

            // An IAsyncEnumerable — a *-bulk endpoint. Nothing has been requested yet; enumerating starts it.
            case not null:
                var element = result.GetType().GetInterfaces()
                    .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                    .GetGenericArguments()[0];
                var take = (Task)TakeMethod.MakeGenericMethod(element).Invoke(null, [result, StreamSample])!;
                await take.ConfigureAwait(false);
                return (IReadOnlyList<object>)Result(take)!;

            default:
                return [];
        }
    }

    private static object? Result(Task task) => task.GetType().GetProperty("Result")!.GetValue(task);

    /// <summary>A list answer becomes its rows; a single-record answer becomes one row, or none when null.</summary>
    private static IReadOnlyList<object> Flatten(object? value) => value switch
    {
        null => [],
        // A workbook is ONE answer, not a row set. byte[] is an IEnumerable, so without this the 1.4 MB
        // financial-reports-xlsx response flattens to 1.4 million boxed bytes — measured 2026-08-27.
        byte[] bytes => bytes.Length > 0 ? [bytes] : [],
        IEnumerable rows => rows.Cast<object?>().Where(r => r is not null).Select(r => r!).ToList(),
        var single => [single],
    };

    private static readonly MethodInfo TakeMethod =
        typeof(Probe).GetMethod(nameof(TakeAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Reads at most <paramref name="max"/> rows, then abandons the download.</summary>
    private static async Task<IReadOnlyList<object>> TakeAsync<T>(IAsyncEnumerable<T> rows, int max)
    {
        var taken = new List<object>();
        await foreach (var row in rows.ConfigureAwait(false))
        {
            if (row is not null) taken.Add(row);
            if (taken.Count >= max) break;
        }
        return taken;
    }

    // ---- shape ---------------------------------------------------------------------------------------------

    /// <summary>The type of one row, from the method's declared return type.</summary>
    public static Type ElementType(Type returnType)
    {
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            return returnType.GetGenericArguments()[0];

        if (typeof(Task).IsAssignableFrom(returnType) && returnType.IsGenericType)
        {
            var inner = returnType.GetGenericArguments()[0];
            // Before the IReadOnlyList probe: byte[] implements IReadOnlyList<byte>, and resolving this to
            // `byte` would make the baseline describe a workbook as a sequence of bytes.
            if (inner == typeof(byte[])) return typeof(byte[]);
            // Prepend(inner) because GetInterfaces() on IReadOnlyList<T> does not include IReadOnlyList<T>.
            var list = inner.GetInterfaces().Prepend(inner)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
            return list?.GetGenericArguments()[0] ?? Nullable.GetUnderlyingType(inner) ?? inner;
        }

        throw new NotSupportedException(
            $"The smoke sweep does not know how to read rows out of {returnType.Name}. Teach ElementType and "
            + "InvokeAsync about it, or the endpoint returning it goes unprobed.");
    }

    /// <summary>The properties whose population is recorded. Empty for a scalar row type — <c>GetSectorsAsync</c>
    /// answers a list of strings, and <c>string.Length</c> is not a field FMP sends.</summary>
    private static IReadOnlyList<PropertyInfo> Fields(Type row) =>
        row == typeof(string) || row.IsPrimitive || row.IsEnum || row.IsArray
            ? []
            : [.. row.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .OrderBy(p => p.Name, StringComparer.Ordinal)];

    /// <summary>Whether a value counts as something FMP sent.
    ///
    /// <para>Blank strings and empty collections count as absent, because that is how this SDK's models spell a
    /// missing value where the CLR type is not nullable: <c>CompanyProfile.Symbol</c> defaults to <c>""</c> and
    /// <c>BulkPeers.Peers</c> to an empty list. Zero counts as present — <c>SharesFloat.FreeFloat</c> is
    /// genuinely <c>0</c> for every ETF, and treating that as missing would record a measured value as absent.</para>
    ///
    /// <para><b>The one blind spot, counted rather than assumed.</b> A non-nullable value-typed property reads as
    /// populated whatever arrives, because its default is a legal value — so a field behind one could stop coming
    /// and this would not see it. Every public property across the models was classified on 2026-08-26 to find out
    /// how much of the surface that is: 752 are nullable, 20 are <c>string</c> defaulting to <c>""</c> and one is
    /// an <c>IReadOnlyList</c> defaulting to empty, all of which this reads correctly. Exactly <b>four</b> are
    /// non-nullable value types. Three are on <see cref="Models.EarningsCalendarResult"/>, which is the list
    /// wrapper rather than a row and is never inspected here. The fourth is
    /// <see cref="Models.AnalystEstimate.Period"/>, which carries <c>[JsonIgnore]</c> — the SDK sets it from the
    /// request, so it is not a field FMP sends and there is nothing for FMP to stop sending. The check therefore
    /// has no blind spot on any wire field the SDK models.</para></summary>
    private static bool Populated(object? value) => value switch
    {
        null => false,
        string text => text.Trim().Length > 0,
        IEnumerable items => items.GetEnumerator().MoveNext(),
        _ => true,
    };

    // ---- arguments -----------------------------------------------------------------------------------------

    /// <summary>Supplies a live-sensible argument, keyed by parameter name where the type alone is ambiguous.
    ///
    /// <para>Every unknown type and every unknown name throws. A default would be worse than a failure here: a
    /// <see langword="false"/> for an unrecognised flag, or a <c>0</c> for an unrecognised number, produces a
    /// call that succeeds against a question nobody meant to ask — and records its answer as the baseline.</para></summary>
    public static object Argument(ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

        if (type == typeof(CancellationToken)) return CancellationToken.None;

        // Dispatched on the parameter NAME, not just the type. Every string here used to become the symbol, which
        // is right for `symbol` and quietly wrong for `exchange`: batch-exchange-quote answers an unknown exchange
        // with an empty array and HTTP 200, so `exchange=AAPL` would have recorded `rows 0` as that endpoint's
        // baseline and matched it happily every week after. SweepCoverageTests cannot catch that — the argument IS
        // synthesisable, it is just meaningless — so the default case has to stay narrow.
        if (type == typeof(string))
            return parameter.Name switch
            {
                "exchange" => LiveApi.Exchange,
                "cik" => LiveApi.Cik,
                "cusip" => LiveApi.Cusip,
                "isin" => LiveApi.Isin,
                "query" => LiveApi.SearchQuery,
                "name" => LiveApi.AcquirerNameQuery,
                "company" => LiveApi.CompanyNameQuery,
                "formType" => LiveApi.FormType,
                "sicCode" => LiveApi.SicCode,
                _ => LiveApi.Symbol,
            };

        // Two symbols rather than one, so a batch endpoint is probed as a batch. See LiveApi.SecondSymbol.
        if (type == typeof(IEnumerable<string>)) return new[] { LiveApi.Symbol, LiveApi.SecondSymbol };

        // OneHour rather than OneMinute: every interval sits inside its own lookback window when asked for a
        // recent range, but the hourly bar answers 434 rows where the minute bar answers 1169 for the same days,
        // and the sweep is measuring shape rather than depth.
        if (type == typeof(ChartInterval)) return ChartInterval.OneHour;

        // Dispatched on NAME, not just type, for the reason the string arm is: `from` and `to` both taking
        // SettledWeekday makes every range one day wide, and a one-day window answers zero rows on anything
        // sparse. See LiveApi.RangeStart for the measurement that forced this.
        //
        // `from` is then dispatched a second time, on the parameter's DECLARING TYPE and, within
        // CalendarEndpoints, on the METHOD NAME, because RangeStart's ninety days is not one safe width for
        // every date-ranged endpoint — it is safe only for the endpoints that were measured to tolerate it, and
        // "the endpoints that were measured to tolerate it" turns out to need three answers rather than two.
        // CalendarEndpoints.GetEarningsCalendarAsync's own doc records day-at-a-time as "the only chunk width
        // measured to be safe": a 31-day window in a heavy month silently truncated at exactly 4000 rows,
        // eating the front of the range with no signal in the body. EconomicsEndpoints.GetEconomicCalendarAsync's
        // own doc measured a 6-month window returning 535 rows — FEWER than the 3-month window it wholly
        // contains — and a −3-to-+12-month window returning 0; "the widest range verified intact here is one
        // week." A 91-day `from` is guaranteed truncated on the first and lands in non-monotonic, unverified
        // territory on the second, so those two keep the narrow, already-settled window. The other five
        // date-ranged CalendarEndpoints methods are sparse enough that even the narrow window is thin — measured
        // 2026-08-28, a single day answered 1 row on ipos-prospectus — so they get LiveApi.CalendarWeekStart, a
        // week wide, instead. Every other `from` — the three sec-filings-search paths this was written for, plus
        // the per-symbol chart and market-cap methods — is unaffected by width and keeps RangeStart.
        if (type == typeof(LocalDate))
            return parameter.Name switch
            {
                // The economic calendar's own doc: "the widest range verified intact here is one week", after a
                // 6-month window returned FEWER rows than the 3-month window it contains and a -3-to-+12-month
                // window returned 0. A week sits exactly on that boundary with no margin, so it keeps the day.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.EconomicsEndpoints)
                    => LiveApi.SettledWeekday,

                // The earnings calendar is the deliberate exception among the Calendar methods, and this arm
                // MUST come before the general one below. Its own doc records day-at-a-time as "the only chunk
                // width measured to be safe": a 7-day peak-season window returned 3676 rows against a 4000-row
                // cap, and a 31-day window returned exactly 4000. Narrowing it was the previous slice's fix.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.CalendarEndpoints)
                    && parameter.Member.Name == nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync)
                    => LiveApi.SettledWeekday,

                // The five other date-ranged Calendar methods are sparse enough that a single day is thin and
                // getting thinner: measured 2026-08-28, one day answered 1 row on ipos-prospectus and 5 on
                // ipos-calendar. A week answers 8 and 34, and keeps dividends-calendar at 41% of its cap rather
                // than the 81% a fortnight would give it. See LiveApi.CalendarWeekStart.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.CalendarEndpoints)
                    => LiveApi.CalendarWeekStart,

                // Everything else -- the three sec-filings-search paths this dispatch was written for, plus the
                // per-symbol chart and market-cap methods -- is unaffected by width and keeps the 90-day range.
                "from" => LiveApi.RangeStart,
                _ => LiveApi.SettledWeekday,
            };
        if (type == typeof(FiscalPeriod)) return FiscalPeriod.Annual;
        // Q1 rather than Annual: the bulk statement family is published per fiscal quarter, and the annual file
        // for SettledYear is not complete until every issuer has filed. A quarter a year old is settled.
        if (type == typeof(BulkFiscalPeriod)) return BulkFiscalPeriod.Q1;
        // A bare criteria object asks for everything; Limit keeps the answer to one page. No filters, because
        // the screener answers an unrecognised filter value with an empty list rather than an error — a typo
        // here would read as "the screener went dark".
        if (type == typeof(ScreenerCriteria)) return new ScreenerCriteria { Limit = 10 };

        if (type == typeof(bool))
            return parameter.Name switch
            {
                // True on purpose: it is what populates ReportTime and the four other extras, so leaving it off
                // would record five properties of EarningsCalendarEntry as never arriving.
                "includeReportTimes" => true,
                // False on purpose: it discards rows, and this suite is measuring what FMP sent.
                "clampToRange" => false,
                _ => throw Unknown(parameter),
            };

        if (type == typeof(int))
            return parameter.Name switch
            {
                "year" => LiveApi.SettledYear,
                "limit" => 5,
                "page" => 0,
                "part" => 0,
                "quarter" => LiveApi.SettledQuarter,
                _ => throw Unknown(parameter),
            };

        throw Unknown(parameter);
    }

    private static NotSupportedException Unknown(ParameterInfo parameter) => new(
        $"The smoke sweep cannot supply a value for '{parameter.Name}' ({parameter.ParameterType.Name}) on "
        + $"{parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}. Add a case to Probe.Argument — "
        + "an endpoint whose arguments cannot be synthesised is an endpoint this suite never calls.");
}
