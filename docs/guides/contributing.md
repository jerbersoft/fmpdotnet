# Contributing

Thanks for looking. This page covers the workflow around a change; **[Development](development.md)** covers building and
testing the code itself.

## The short version

1. Open an issue first for anything larger than a typo.
2. Branch from `master`.
3. **Measure the live API before modelling it.**
4. Commit in conventional-commit form, referencing the issue.
5. Open a PR. Wait for `.NET — build + test` to go green.
6. Merge.

## The one principle

**A claim in this repository should have a measurement behind it.**

That is not a style preference — it is where most of the value here came from. FMP's documentation does not
describe several behaviours the SDK has to handle: an endpoint that truncates silently at 4000 rows, a bulk error
returned under HTTP 200, a `limit` cap that is never mentioned, two timestamp fields in opposite timezones under
one identical wire format, a 404 whose body is a valid success shape.

None of those could have been read. All of them were probed.

So a PR that adds an endpoint is expected to say **what was measured and how**, and a documentation claim without
evidence behind it will be asked for evidence. Several commits in the history exist purely to correct claims a
review found overstated — that is the standard, not an exception.

## Branch, PR, merge

`master` carries a repository ruleset. In practice:

* **A pull request is required.** Direct pushes are rejected with `GH013`.
* **Zero approvals required**, so a solo change is not blocked — but the PR itself is.
* **`.NET — build + test` must pass**, by name.
* **Force-push and deletion are blocked.**
* The repository admin role is on the bypass list, so a direct push is *possible* deliberately. It is an escape
  hatch, not the route.

Branches are built on **every push**, not only when a PR is open — work here happens on feature branches that may
sit a while, and CI that only ran at PR time would leave the branch unverified for exactly the period when the
feedback is worth most.

### Branch naming

Follow what is already there — a type prefix and a slice name:

```
feat/statements-coverage
docs/endpoint-inventory
fix/market-cap-decimal
```

## Commits

Conventional commits, lowercase, **saying what changed rather than what area was touched**, with the issue in
parentheses:

```
feat: model the two mergers-and-acquisitions paths (#29)
fix: market cap is fractional on stable/profile, so it cannot be a long (#29)
docs: the period vocabulary is six values, not five (#28)
test: re-record the ordinary baseline with the twenty new Statements methods (#28)
chore: ignore .env variants and the developer bulk cache
```

Prefixes in use: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `build`, `ci`.

**Breaking changes take `!`** — `refactor!:` — and there have been two. Both *removed* public members after
measurement showed they were the wrong shape. Until 1.0 that is expected; see
**[Releases and Versioning](releases-and-versioning.md)**.

The subject line is doing real work in this repository. `fix: market cap is fractional on stable/profile, so it
cannot be a long` tells you the finding *and* the consequence. `fix: market cap type` tells you neither.

## Adding an endpoint

The full checklist is in **[Development](development.md)**. The three steps people skip:

**Regenerate the coverage table.** `FMPDOTNET_UPDATE_README=1 dotnet test`, then commit the README change
alongside the code. The build fails without it.

**Add it to the live sweep.** An endpoint the sweep skips is an endpoint whose renamed field goes unnoticed until
a consumer hits it. An offline test enforces this, so forgetting it fails the build.

**Re-record the smoke baseline**, after reading the diff. See **[Live Smoke Suite](live-smoke-suite.md)**.

## Design conventions

Worth knowing before proposing a surface, because each of these has a reason and the reason is not taste:

* **Everything throws.** No `Try`-prefixed methods, no sentinel returns. `null` means "an answer FMP gave", never
  "a failure". See **[Error Handling](error-handling.md)**.
* **NodaTime only** in public signatures. No `DateTime`, `DateOnly`, `DateTimeOffset` or `TimeSpan`.
* **Nullable models, nothing `required`.** A `required` property turns a rename into an exception that costs the
  caller the whole response.
* **`decimal` over `long`/`int`** for anything numeric from the wire, unless you have measured that it cannot be
  fractional.
* **An enum wherever FMP takes a fixed vocabulary**, so a typo is a compile error rather than an HTTP 200 with no
  rows.
* **No reflection.** The library declares `IsAotCompatible`; `IL2026` and `IL3050` are build errors. Use the
  source-generated JSON context.
* **No tier map.** Entitlement moves and varies per key — anything claiming "this needs Ultimate" would be
  confidently wrong sooner or later.

## Documentation

The README is the canonical reference and is expected to change with the code. In particular:

* Anything surprising the API does belongs in the **upstream behaviour** section, **with the measurement that
  established it and the date**.
* The **coverage table is generated**. Never hand-edit the block between its markers.
* The guides on this site hold the how-to and the process. They deliberately **link into the README** for measured
  numbers rather than restating them, so there is only ever one copy to keep true. Please keep it that way.

**Where a fact lives.** One canonical copy of each, and a change to behaviour lands in the same pull request as the
change to the page describing it:

| Content | Home |
|---|---|
| Guides, runbooks, troubleshooting, FAQ | `docs/guides/`, on this site |
| Measured upstream behaviour, the endpoint table, the registration paths, versioning | `README.md`, rendered on this site as [Reference](../../README.md) |
| API | The XML doc comments; the [API reference](../api/index.md) renders them and does not restate them |
| Changelog | [`docs/changelog.md`](../changelog.md) |
| Designs, plans and measurements | `docs/superpowers/`, never published |

The site is built with `--warningsAsErrors`, so a link to a README section that no longer exists fails the build
rather than becoming a dead link nobody reports. [Development](development.md) has the build command.

## Reporting a problem

[Open an issue](https://github.com/jerbersoft/fmpdotnet/issues) with:

* the **endpoint** and the method you called;
* the **exception type**, and `StatusCode` if there was one — for `FmpApiException` a **null** `StatusCode` is
  itself meaningful, and for `FmpPlanRestrictedException` 402 and 403 mean different things;
* what you expected versus what arrived.

Please check **[Troubleshooting](troubleshooting.md)** first — a good number of surprises on this API are already
recorded there with the measurement behind them.

**Never paste an API key**, including inside a URL. The SDK redacts keys from its own exception messages and
request renderings, so an unmodified stack trace is safe to share.

## Security

Please do **not** open a public issue for a security problem. Report it privately through the repository's
security advisories, or contact the maintainer directly.

## Code of conduct

Be decent. Assume the other person measured something you have not, and ask what it was.
