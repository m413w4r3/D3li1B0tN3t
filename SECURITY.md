# Security policy

## Supported versions

D3B updates itself to its latest release, and only that release gets security fixes. Fixes land on `main` first, then
in the next release, which installed apps download when they start.

| Version | Supported |
| --- | --- |
| The latest release | Yes |
| Older releases | No: update from the app's banner or settings page |
| `main` | Fixed there first |

## Reporting a vulnerability

**Do not report a vulnerability in a public issue, pull request or comment.**

Report it privately through GitHub: on the repository's [Security](https://github.com/AerafalDev/D3B/security) tab,
choose *Report a vulnerability*. Only the maintainer sees the report, and you can discuss the fix there.

Please include:

- what an attacker can do, and what they need first, such as running a program on the same machine, sending a crafted
  file, or controlling a network;
- the steps to reproduce it, with the smallest input that shows it;
- D3B's version or commit, and the client build, both on the app's settings page;
- whether you want to be credited, and under which name.

Never include a real capture, database or login ticket. If the issue needs one, describe its structure with
`scripts/capture.cs`, or make one on an account you can afford to lose.

## What matters most in D3B

D3B relays a game client's connection, keeps data about accounts, and updates itself. Reports about these are the most
useful:

- **The relay.** It listens on a loopback address and forwards the client's connection to Ankama's servers. Anything
  that makes it reachable from another machine, sends the client or its traffic somewhere other than Ankama's servers,
  or lets another program inject frames into a bot's connection.
- **Login tickets.** The relay sees the account's login ticket. D3B keeps it in memory only, never logs it, and the
  protocol inspector hides it; only captures, which are off by default, record it. Any path through which a ticket
  leaks into a log, a page of the app, an error message, a toast or a file other than a capture.
- **The client's configuration.** While D3B runs, it rewrites one line of the client's `version` file and writes a copy
  of Ankama's configuration under `%LOCALAPPDATA%\D3B\client`. Anything that lets another party choose what that copy
  says, or leaves the client pointed somewhere unexpected once D3B stops.
- **Untrusted input.** Frames from the server or the client, captures opened in the inspector, and packs, schemas and
  mappings, which the tools download and check against their SHA-256. A crafted input that crashes the app, hangs it,
  or makes it read or write outside what it should.
- **Updates and releases.** The app installs updates from this repository's releases through Velopack, and the release
  workflow builds them on a tag. Anything that lets someone other than the maintainer publish an update an installed
  app would take.

## Out of scope

- **Ankama's game, client, servers and protections.** Report those to Ankama, not here.
- **Getting banned.** Using a bot breaches Ankama's terms of use, and D3B does nothing to hide it
  ([D2](docs/decisions.md#d2-what-d3b-never-contains)). Ways for Ankama to detect D3B are not vulnerabilities.
- **An attacker who already runs code as your Windows user.** Such a program can read and change everything D3B keeps.
- **The unsigned installer.** SmartScreen warns about it, as the README says; signing waits for a certificate.

## What happens next

D3B is maintained by one person in their free time. You can expect:

- an acknowledgment within a week;
- an assessment, and a fix or a decision not to fix with its reasons, discussed with you in the private report;
- a release with the fix, then the advisory published, crediting you unless you prefer otherwise.

Please give a reasonable time to ship the fix before disclosing the issue publicly. Thank you for helping keep D3B's
users safe.
