## What and why

<!-- What this pull request changes and why. Link the issue it closes, such as "Closes" followed by the issue's link. -->

## How it was checked

<!-- The commands run, and for behavior that only shows in the game: the client build, what you did and what you saw. -->

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] `dotnet format --verify-no-changes`
- [ ] Checked in the game, as described above

## Checklist

- [ ] It follows the conventions of [CONTRIBUTING.md](https://github.com/AerafalDev/D3B/blob/main/CONTRIBUTING.md).
- [ ] Changes to `src/` come with their tests.
- [ ] The README and `docs/` describe the new behavior, and a change of direction has its entry in `docs/decisions.md`.
- [ ] Nothing extracted from the client, no capture, no database and no login ticket is committed.
- [ ] Nothing it adds is among what D3B never does (D2).
