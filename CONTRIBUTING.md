# Contributing to FmpDotNet

Thanks for looking. This is the short version; the full guide lives in the
**[wiki](https://github.com/jerbersoft/fmpdotnet/wiki/Contributing)**.

## The one principle

**A claim in this repository should have a measurement behind it.**

That is not a style preference — it is where most of the value here came from. FMP's documentation does not
describe several behaviours the SDK has to handle: an endpoint that truncates silently at 4000 rows, a bulk error
returned under HTTP 200, a `limit` cap that is never mentioned, two timestamp fields in opposite timezones under
one identical wire format, a 404 whose body is a valid success shape.

None of those could have been read. All of them were probed.

So a pull request that adds an endpoint is expected to say **what was measured and how**, and a documentation
claim without evidence behind it will be asked for evidence.

## Getting set up

```bash
git clone https://github.com/jerbersoft/fmpdotnet.git
cd fmpdotnet
dotnet restore FmpDotNet.slnx
dotnet build   FmpDotNet.slnx
dotnet test    FmpDotNet.slnx
```

**No API key is needed.** Every live test skips itself when `FMP_API_KEY` is unset, so a fresh clone runs the
whole solution green and offline.

## Workflow

1. Open an issue first for anything larger than a typo.
2. Branch from `master` — `feat/`, `fix/` or `docs/` plus a slice name.
3. **Measure the live API before modelling it.**
4. Commit in conventional-commit form, referencing the issue.
5. Open a pull request and wait for **`.NET — build + test`** to go green.
6. Merge.

`master` requires a pull request and that named check. Force-push and deletion are blocked.

## Three steps people skip

**Regenerate the coverage table.** The README's endpoint table is generated from the code, and the build fails
without it:

```bash
FMPDOTNET_UPDATE_README=1 dotnet test
```

**Add the endpoint to the live sweep.** An endpoint the sweep skips is one whose renamed field goes unnoticed
until a consumer hits it. An offline test enforces this, so forgetting it fails the build.

**Re-record the smoke baseline**, after reading the diff —
see [Live Smoke Suite](https://github.com/jerbersoft/fmpdotnet/wiki/Live-Smoke-Suite).

## Design conventions

Each of these has a reason, and the reason is not taste:

* **Everything throws.** No `Try`-prefixed methods, no sentinel returns. `null` means "an answer FMP gave", never
  "a failure".
* **NodaTime only** in public signatures — no `DateTime`, `DateOnly`, `DateTimeOffset` or `TimeSpan`.
* **Nullable models, nothing `required`.** A `required` property turns a rename into an exception that costs the
  caller the whole response.
* **`decimal` over `long`/`int`** for anything numeric off the wire, unless you have measured that it cannot be
  fractional.
* **An enum wherever FMP takes a fixed vocabulary**, so a typo is a compile error rather than an HTTP 200 with no
  rows.
* **No reflection.** The library declares `IsAotCompatible`; `IL2026` and `IL3050` are build errors.
* **No tier map.** Entitlement moves and varies per key.

## Reporting a problem

[Open an issue](https://github.com/jerbersoft/fmpdotnet/issues) with the endpoint, the exception type, and its
`StatusCode` if there was one — for `FmpApiException` a **null** `StatusCode` is itself meaningful, and for
`FmpPlanRestrictedException` 402 and 403 mean different things.

Please check [Troubleshooting](https://github.com/jerbersoft/fmpdotnet/wiki/Troubleshooting) first.

**Never paste an API key**, including inside a URL. The SDK redacts keys from its own exception messages and
request renderings, so an unmodified stack trace is safe to share.

For a security problem, see [SECURITY.md](SECURITY.md) — please do not open a public issue.

## More

| | |
|---|---|
| Full contributing guide | [wiki/Contributing](https://github.com/jerbersoft/fmpdotnet/wiki/Contributing) |
| Building, testing, adding an endpoint | [wiki/Development](https://github.com/jerbersoft/fmpdotnet/wiki/Development) |
| How the pieces fit | [wiki/Architecture](https://github.com/jerbersoft/fmpdotnet/wiki/Architecture) |
| The live API sweep | [wiki/Live-Smoke-Suite](https://github.com/jerbersoft/fmpdotnet/wiki/Live-Smoke-Suite) |
