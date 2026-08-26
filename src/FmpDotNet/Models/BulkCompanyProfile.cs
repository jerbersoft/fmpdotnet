using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One company's profile row from <c>stable/profile-bulk</c>, the CSV download that carries the whole
/// universe a part at a time.
///
/// <para><b>Why this is not <see cref="CompanyProfile"/>.</b> The two carry the same 36 field names, so folding them
/// into one type is the obvious move — and it is wrong for three measured reasons, each of which would cost a
/// caller something:</para>
/// <list type="number">
/// <item><description><b>The same name means a different number.</b> <c>volume</c> on <c>stable/profile</c> is the
/// session's share count and <see cref="CompanyProfile.Volume"/> types it <see langword="long"/>. On
/// <c>profile-bulk</c> the same column arrives fractional — <c>73305.59636</c> on <c>PRTA</c> and
/// <c>60854.19398</c> on <c>PRDO</c>, measured 2026-08-26 — because it is an <i>averaged</i> figure despite the
/// name. One type would have to widen both to <see langword="decimal"/> and lose the fact that the per-symbol
/// endpoint really does return whole shares, or narrow both and throw away the fractions. See
/// <see cref="Volume"/>.</description></item>
/// <item><description><b>The two are read by different machinery.</b> <see cref="CompanyProfile"/> is deserialised
/// from JSON through the source-generated <c>FmpJsonContext</c> and carries a <c>[JsonPropertyName]</c> on every
/// property. This type is mapped positionally out of a CSV record by <see cref="FromCsv"/>, is never touched by
/// <c>System.Text.Json</c>, and must not be registered in the JSON context. Attributes that steer a deserialiser
/// this type never meets are dead weight that reads as a promise.</description></item>
/// <item><description><b>They differ in what a null means.</b> In JSON an absent field and an explicit
/// <c>null</c> are distinguishable; in CSV an empty field is the only way to say "no value", so
/// <c>cusip</c> being blank on <c>PRTA</c> and <c>cik</c> and <c>phone</c> being blank on <c>MRV.TO</c> all read
/// as <see langword="null"/> here with no way to tell "absent" from "empty". A shared type would quietly export
/// the weaker of the two guarantees to callers of both.</description></item>
/// </list>
///
/// <para><b>Column count.</b> Measured 2026-08-26 on <c>part=0</c>: the header carries <b>36</b> columns, in the
/// order the properties below are declared. An earlier note recorded 28 by stopping the enumeration at
/// <c>state</c>; the eight beyond it — <c>zip, image, ipoDate, defaultImage, isEtf, isActivelyTrading, isAdr,
/// isFund</c> — are present on every row of the capture and are mapped here. <c>part=1</c> answered the identical
/// header.</para>
///
/// <para><b>Wire shape.</b> Quoted and bare fields are mixed inside one record —
/// <c>"PRTA",9.18,480602716,-0.354,0,"7.73-11.8",…</c> — and <see cref="Description"/> runs to 1,592 characters
/// containing commas, apostrophes and typographic quotes. <see cref="CsvStreamReader"/> handles all of that; the
/// mapping below only has to name columns.</para></summary>
public sealed record BulkCompanyProfile
{
    /// <summary>Ticker as FMP spells it. Not US-only: the measured part-0 rows include <c>MRV.TO</c> on the Toronto
    /// exchange, and the universe reaches into every venue FMP covers.</summary>
    public required string Symbol { get; init; }

    /// <summary>Latest trade price in <see cref="Currency"/>.</summary>
    public decimal? Price { get; init; }

    /// <summary>Market capitalisation, in <see cref="Currency"/>.
    ///
    /// <para><see langword="decimal"/> rather than the <see langword="long"/> that
    /// <see cref="CompanyProfile.MarketCap"/> uses. CSV carries no integer/float distinction to preserve, and the
    /// repo's rule is that money is <see langword="decimal"/>; a value that arrived as <c>3.4e10</c> would parse
    /// here and throw there.</para></summary>
    public decimal? MarketCap { get; init; }

    /// <summary>Beta against the broad market. <c>0</c> is a real measured value (<c>MRV.TO</c>), not an absent
    /// one — an absent one is <see langword="null"/>.</summary>
    public decimal? Beta { get; init; }

    /// <summary>Most recent dividend per share. <c>0</c> where the issuer pays none.</summary>
    public decimal? LastDividend { get; init; }

    /// <summary>52-week range as FMP renders it, and it is a <b>string</b>, not a number:
    /// <c>"7.73-11.8"</c>, <c>"26.66-38.5"</c>, <c>"26-53.75"</c> are the three measured values. Parsing it as a
    /// number silently yields the low bound minus the high one on any parser lenient enough to try. Callers that
    /// want the two bounds split them on the first <c>-</c> — and must watch for negative bounds, which the format
    /// gives no way to disambiguate.</summary>
    public string? Range { get; init; }

    /// <summary>Absolute price change on the session, in <see cref="Currency"/>.</summary>
    public decimal? Change { get; init; }

    /// <summary>Fractional price change on the session — <c>-2.54777</c> means -2.54777%, not -254%.</summary>
    public decimal? ChangePercentage { get; init; }

    /// <summary><b>Not a session share count, and not an integer.</b> Measured 2026-08-26 this column arrives
    /// fractional — <c>73305.59636</c> for <c>PRTA</c>, <c>60854.19398</c> for <c>PRDO</c> — while
    /// <c>MRV.TO</c> on the same part answered a bare <c>37760</c>. A whole number here is therefore a coincidence
    /// of rounding, not a guarantee, which is why this is <see langword="decimal"/> and not
    /// <see langword="long"/>: a <see langword="long"/> mapping would drop the fraction on most rows and read
    /// clean while doing it.
    ///
    /// <para>The fractions say what the figure is: an <i>average</i> over some recent window, not the volume of a
    /// single session, despite sharing the name of <see cref="CompanyProfile.Volume"/>, which on the per-symbol
    /// JSON endpoint is the session count. Do not compare the two across the two endpoints as though they measured
    /// the same thing.</para></summary>
    public decimal? Volume { get; init; }

    /// <summary>Average daily volume. Integral on all three measured rows, but <see langword="decimal"/> for the
    /// same reason as <see cref="Volume"/> — an averaged figure has no promise of being whole, and the type is
    /// cheaper to widen now than after a caller has stored it.</summary>
    public decimal? AverageVolume { get; init; }

    /// <summary>Registered company name. Contains commas on real rows (<c>"Marvell Technology, Inc."</c>), so it is
    /// always quoted in the payload.</summary>
    public string? CompanyName { get; init; }

    /// <summary>Reporting currency, ISO 4217. Not always <c>USD</c>: <c>MRV.TO</c> reports <c>CAD</c>. Every
    /// monetary field on the row is denominated in this, so aggregating <see cref="MarketCap"/> across the
    /// universe without converting first mixes currencies silently.</summary>
    public string? Currency { get; init; }

    /// <summary>SEC Central Index Key, zero-padded to ten digits (<c>"0001559053"</c>) and therefore text, not a
    /// number — as a number it loses its leading zeros and stops matching EDGAR. Null on non-SEC registrants;
    /// <c>MRV.TO</c> answered empty.</summary>
    public string? Cik { get; init; }

    /// <summary>ISIN.</summary>
    public string? Isin { get; init; }

    /// <summary>CUSIP, or <see langword="null"/> where FMP holds none — the field is <b>empty</b> rather than
    /// absent, measured on <c>PRTA</c>, and an empty CSV field reads as null here.</summary>
    public string? Cusip { get; init; }

    /// <summary>Listing exchange, long form (<c>NASDAQ Global Select</c>, <c>Toronto Stock Exchange</c>). Equal to
    /// <see cref="Exchange"/> on some rows (<c>PRTA</c> answered <c>NASDAQ</c> for both), so it cannot be relied on
    /// to be the more specific of the two.</summary>
    public string? ExchangeFullName { get; init; }

    /// <summary>Listing exchange, short form (<c>NASDAQ</c>, <c>TSX</c>).</summary>
    public string? Exchange { get; init; }

    /// <summary>FMP's industry label. The permitted values are the ones <c>stable/available-industries</c> lists —
    /// see <c>DirectoryEndpoints.GetIndustriesAsync</c> rather than hard-coding a set.</summary>
    public string? Industry { get; init; }

    /// <summary>Corporate website. Scheme varies (<c>https://</c> on two measured rows, <c>http://</c> on the
    /// third); it is not normalised.</summary>
    public string? Website { get; init; }

    /// <summary>Business description, up to at least 1,592 characters on the measured rows. This one field is most
    /// of the 30.4 MB a part weighs — a caller that only wants the classification fields still pays to stream
    /// it.</summary>
    public string? Description { get; init; }

    /// <summary>Chief executive.</summary>
    public string? Ceo { get; init; }

    /// <summary>FMP's sector label, from <c>stable/available-sectors</c>.</summary>
    public string? Sector { get; init; }

    /// <summary>Country of domicile, ISO 3166-1 alpha-2. Reflects the issuer rather than the listing venue —
    /// <c>MRV.TO</c> lists in Toronto and answers <c>US</c> — so filtering a US universe on this is not the same as
    /// filtering on <see cref="Exchange"/>.</summary>
    public string? Country { get; init; }

    /// <summary>Headcount as reported, kept as text rather than parsed to a number, matching
    /// <see cref="CompanyProfile.FullTimeEmployees"/>. It is an issuer-reported estimate that some file as a range,
    /// and a caller that wants a number can parse one — a caller that wants to display the value as filed cannot
    /// un-parse it.</summary>
    public string? FullTimeEmployees { get; init; }

    /// <summary>Switchboard number, unnormalised and null where FMP holds none (<c>MRV.TO</c>).</summary>
    public string? Phone { get; init; }

    /// <summary>Street address.</summary>
    public string? Address { get; init; }

    /// <summary>City.</summary>
    public string? City { get; init; }

    /// <summary>State or province.</summary>
    public string? State { get; init; }

    /// <summary>Postal code, text rather than a number — <c>"D02 VK60"</c> is a measured Irish Eircode and
    /// <c>"60173"</c> a US ZIP that would lose nothing here but does elsewhere.</summary>
    public string? Zip { get; init; }

    /// <summary>Logo URL. Check <see cref="DefaultImage"/> before treating it as a real logo.</summary>
    public string? Image { get; init; }

    /// <summary>IPO date. Plain <c>yyyy-MM-dd</c> with no time component, so <see cref="LocalDate"/> and not
    /// <see cref="Instant"/>.</summary>
    public LocalDate? IpoDate { get; init; }

    /// <summary>True when <see cref="Image"/> is FMP's placeholder rather than a real logo — <c>MRV.TO</c> answered
    /// true while still carrying an image URL, so the URL alone does not tell you.</summary>
    public bool? DefaultImage { get; init; }

    /// <summary>True when the security is an ETF.</summary>
    public bool? IsEtf { get; init; }

    /// <summary>True while the security still trades. Going false is how a delisting first shows up here, and on a
    /// whole-universe download it is the cheapest way to find the delistings since the last run.</summary>
    public bool? IsActivelyTrading { get; init; }

    /// <summary>True when the security is an ADR.</summary>
    public bool? IsAdr { get; init; }

    /// <summary>True when the security is a fund.</summary>
    public bool? IsFund { get; init; }

    /// <summary>Maps one CSV record. Column lookup is by name and case-insensitive, so a column FMP reorders still
    /// lands on the right property and a column it drops reads as null rather than shifting every field after
    /// it.</summary>
    internal static BulkCompanyProfile FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Price = row.GetDecimal("price"),
        MarketCap = row.GetDecimal("marketCap"),
        Beta = row.GetDecimal("beta"),
        LastDividend = row.GetDecimal("lastDividend"),
        Range = row.GetString("range"),
        Change = row.GetDecimal("change"),
        ChangePercentage = row.GetDecimal("changePercentage"),
        Volume = row.GetDecimal("volume"),
        AverageVolume = row.GetDecimal("averageVolume"),
        CompanyName = row.GetString("companyName"),
        Currency = row.GetString("currency"),
        Cik = row.GetString("cik"),
        Isin = row.GetString("isin"),
        Cusip = row.GetString("cusip"),
        ExchangeFullName = row.GetString("exchangeFullName"),
        Exchange = row.GetString("exchange"),
        Industry = row.GetString("industry"),
        Website = row.GetString("website"),
        Description = row.GetString("description"),
        Ceo = row.GetString("ceo"),
        Sector = row.GetString("sector"),
        Country = row.GetString("country"),
        FullTimeEmployees = row.GetString("fullTimeEmployees"),
        Phone = row.GetString("phone"),
        Address = row.GetString("address"),
        City = row.GetString("city"),
        State = row.GetString("state"),
        Zip = row.GetString("zip"),
        Image = row.GetString("image"),
        IpoDate = row.GetDate("ipoDate"),
        DefaultImage = row.GetBoolean("defaultImage"),
        IsEtf = row.GetBoolean("isEtf"),
        IsActivelyTrading = row.GetBoolean("isActivelyTrading"),
        IsAdr = row.GetBoolean("isAdr"),
        IsFund = row.GetBoolean("isFund"),
    };
}
