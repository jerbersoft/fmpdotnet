using FmpDotNet.Http;

namespace FmpDotNet.Tests;

/// <summary>The API key must not survive into a log line, a filename or an exception message. Three handlers now
/// render a built URI — the timeout handler into an exception, the developer bulk cache into a filename and two
/// log lines, and the retry handler into a warning — and all three go through this one helper, so a hole here is a
/// hole in every one of them.</summary>
public sealed class UriRedactionTests
{
    private const string Key = "super-secret-key";

    [Fact]
    public void Replaces_the_key_and_keeps_the_rest_of_the_query()
    {
        var redacted = UriRedaction.Redact(
            new Uri($"https://financialmodelingprep.com/stable/profile?symbol=AAPL&apikey={Key}"));

        Assert.DoesNotContain(Key, redacted);
        Assert.Contains("[redacted]", redacted);
        // The rest of the query is what makes a log line useful; dropping it all to be safe would trade the whole
        // diagnostic for a secret that can be removed on its own.
        Assert.Contains("symbol=AAPL", redacted);
    }

    [Fact]
    public void A_parameter_whose_name_merely_ends_in_apikey_does_not_shadow_the_real_one()
    {
        // FmpRequest is public so a caller can reach an endpoint the SDK has not modelled yet, which means
        // parameter names are caller-controlled — and the key is appended LAST, after all of them. A redactor that
        // searched for the first `apikey=` substring would match inside `xapikey=`, redact the decoy, and leave
        // the credential in the log.
        var redacted = UriRedaction.Redact(
            new Uri($"https://financialmodelingprep.com/stable/profile?xapikey=decoy&apikey={Key}"));

        Assert.DoesNotContain(Key, redacted);
    }

    [Fact]
    public void A_caller_supplied_apikey_parameter_is_redacted_as_well_as_the_appended_one()
    {
        // Both are real query parameters and both carry a credential. Redacting only the first would leave the
        // one the transport appended — the one that actually authenticates.
        var redacted = UriRedaction.Redact(
            new Uri($"https://financialmodelingprep.com/stable/profile?apikey=first-key&symbol=AAPL&apikey={Key}"));

        Assert.DoesNotContain(Key, redacted);
        Assert.DoesNotContain("first-key", redacted);
        Assert.Contains("symbol=AAPL", redacted);
    }

    [Fact]
    public void Redacts_the_key_when_it_is_the_only_parameter()
    {
        var redacted = UriRedaction.Redact(new Uri($"https://financialmodelingprep.com/stable/profile?apikey={Key}"));

        Assert.Equal("https://financialmodelingprep.com/stable/profile?apikey=[redacted]", redacted);
    }

    [Fact]
    public void Leaves_a_uri_that_carries_no_key_untouched()
    {
        const string plain = "https://financialmodelingprep.com/stable/profile?symbol=AAPL";

        Assert.Equal(plain, UriRedaction.Redact(new Uri(plain)));
    }

    [Fact]
    public void A_null_uri_renders_as_empty_rather_than_throwing()
    {
        // The call sites reach here while already reporting a failure; adding an exception to that is the one
        // thing a redactor must not do.
        Assert.Equal(string.Empty, UriRedaction.Redact(null));
    }
}
