# Security Policy

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Report it privately through GitHub's private vulnerability reporting:

**[Report a vulnerability →](https://github.com/jerbersoft/fmpdotnet/security/advisories/new)**

That opens a private advisory visible only to you and the maintainer, and it can be converted into a published
advisory and a CVE once a fix is out.

If that page is unavailable to you, open a regular issue containing **no detail** — just a request for a private
channel — and you will be contacted.

### What to include

* The affected version — a package version (`0.1.0-ci.N`) or a commit SHA.
* What an attacker can do, and what they need in order to do it.
* A minimal reproduction if you have one.

**Never include a real API key**, in a URL or anywhere else. The SDK redacts keys from its own exception messages
and request renderings, so an unmodified stack trace is safe to attach. If you believe you have exposed a key
while testing, rotate it with FMP first.

### What to expect

This is a solo-maintained project, so response is **best effort** rather than a guaranteed window. You will get an
acknowledgement, an assessment of whether it is in scope, and — if it is — notice before any fix is published.

Please give a reasonable window for a fix before disclosing publicly.

## Supported versions

| Version | Supported |
|---|---|
| Latest `0.1.0-ci.N` prerelease on `master` | ✅ |
| Older `ci.N` prereleases | ❌ — fixes land on `master` and publish as a new prerelease |

**No stable release has been cut yet.** Everything published so far is a CI prerelease; see
[Releases and Versioning](https://jerbersoft.github.io/fmpdotnet/guides/releases-and-versioning.html). Until 1.0, the
supported version is simply the newest one.

## Scope

This is a client library. It makes outbound HTTPS requests to Financial Modeling Prep, deserializes the responses,
and holds an API key. Things that are **in scope**:

* **API key disclosure** — the key is sent as an `apikey` request header, so no URL the SDK builds carries it,
  and `FmpRequest.ToString()`, exception messages and the developer cache's filenames are key-free by
  construction. A key a caller pastes into a request path (`?apikey=…`, the form FMP's documentation shows) does
  land on the URL, and the three handlers that render one still redact it. **A path that leaks it anywhere is a
  vulnerability**, and one has been fixed before (`fix: keep the API key out of the timeout exception message`,
  from when the key was still a query parameter).
* **Denial of service against the consuming host** — unbounded memory on a large response, or an upstream-supplied
  value that can stall the process. `Retry-After` is clamped by `MaxRetryAfter` for exactly this reason: it is an
  upstream-controlled value that halts every FMP call in the process.
* **Deserialization flaws** reachable from a response body.
* **Path or query injection** through a caller-supplied symbol or parameter.

**Out of scope**, though still worth an ordinary issue:

* Vulnerabilities in Financial Modeling Prep's own API or website — report those to
  [FMP](https://site.financialmodelingprep.com/), not here.
* Behaviour of a dependency, unless this SDK's use of it is what creates the problem. Advisories against
  dependencies already **fail the build** — `TreatWarningsAsErrors` covers `NU*` codes, so a newly published
  advisory against any package, direct or transitive, breaks CI rather than adding a line nobody reads.
* `DeveloperBulkCacheDirectory` serving stale data. That is
  [documented, warned-about behaviour](https://jerbersoft.github.io/fmpdotnet/guides/rate-limits-and-bulk-data.html) of a
  development aid that is off by default and logs a warning the first time it serves anything.
* Anything requiring the attacker to already control the configuration or the process.

## Handling your own key

Not a vulnerability class, but the most likely way an incident actually happens:

* The key travels as an `apikey` **request header**. FMP also accepts it as a `?apikey=` query parameter — every
  example URL in its documentation carries one — but a key on a URL reaches logs, browser history and crash
  reports, so do not paste that form into an `FmpRequest` path. Treat any URL that carries one as sensitive.
* `.env` and its variants are git-ignored. Keep it that way.
* In CI, use a secret. The live smoke workflow reads `FMP_API_KEY` from repository secrets and fails loudly when
  it is missing, rather than skipping quietly.
* FMP restricts keys it considers abusive — particularly on the `*-bulk` endpoints. A sudden `403` may mean your
  key was restricted rather than that your plan changed; see
  [Error Handling](https://jerbersoft.github.io/fmpdotnet/guides/error-handling.html).
