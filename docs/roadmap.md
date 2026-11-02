# Roadmap

Steps in order. A step takes one or more sessions, and every new large task starts in a new session (D17). A step is
done when its code, the tests it needs and its docs are in, and every check is green. Decisions go to
[decisions.md](decisions.md). The "Settles" line of a step lists the open questions it closes.

| # | Step | Status |
| --- | --- | --- |
| 0 | Foundation: brief, decisions, architecture, roadmap | Done |
| 1 | Scaffold and modules | Done |
| 2 | Game data | Done |
| 3 | Protocol and connection | Done; Q10 at the next client update |
| 4 | Bot run time and navigation | Done |
| 5 | Desktop app, first version | Done |
| 6 | Harvesting and banking: first milestone | Milestone reached in 6a; icons and learned graphics in 6b; what 6a left in 6c; the app redesigned in 6d; the owner's settings in 6e; the app published in 6f |
| 7 | Mods: Lua scripting, then blueprints | Lua done, then removed (D119); blueprints to do, after step 9 (D117, D169) |
| 8 | MITM mode | Dropped: merged into step 3 (D23) |
| 9 | Fights | In progress, before the blueprints (D169): 9a to 9e3, 9f's states, 9f2's summons, 9f3's bombs, 9f4's marks, 9g to 9g6 done, one AI for every character (D244), Incarnam left through its portal (D253); next walls and glyphs weighed over the rounds they strike (D286), then the Féca's glyphs and 9f's other mechanisms |
| 10 | Several accounts and groups | To do |
| 11 | Marketplace and crafting | To do |
| 12 | Treasure hunts and dungeons | To do |
| 13 | The owner's profession scripts | To do |

## 0. Foundation

Done on 2026-10-27. The references in `vendor/` and the bots sold for Dofus 3 were studied, and decisions D1 to D18
were taken one by one with the owner. No code.

## 1. Scaffold and modules

The solution and the module system, with no game logic. Done on 2026-10-27, with decisions D19 to D22.

- `global.json`, `Directory.Build.props`, `Directory.Packages.props` (central package management), `.editorconfig`
  enforcing the C# style of `CLAUDE.md`, warnings as errors, MinVer.
- The projects of the [architecture](architecture.md#projects), empty except `D3B.Core`.
- `D3B.Core`: `IModule`, `BotBuilder` on the Generic Host, module requirements and deduplication, the default group,
  plugin loading from `plugins/` (D5, D6).
- `D3B.Host` starts, builds the modules and stops cleanly.
- The plugin template in `templates/`, and one sample plugin loaded by a test.
- Test projects on xunit.v3 and Microsoft.Testing.Platform, and CI on GitHub Actions under Windows.

## 2. Game data

Done on 2026-10-27, with decisions D24 to D33. Settles Q4, settled by D24 in session 2a.

- The inspection and extraction tools in `scripts/`, on the Unity readers copied from Dofus Remastered (D10).
- The pack format, MemoryPack and zstd, written by the extraction tool and read by `D3B.Data`.
- What the first milestone needs: maps and cells, the world graph with zaaps and transitions, interactives, harvestable
  resources, items, professions, banks, texts in French and English.

Four sessions:

| Session | Content | Status |
| --- | --- | --- |
| 2a | Foundations: `scripts/unity/`, `inspect.cs`, the pack format in `D3B.Data` with its tests, `extract.cs` writing the texts, Q4 (D24 to D26). | Done on 2026-10-27 |
| 2b | Maps and the world graph: Dofus Remastered's typed readers, cells, interactive elements placed on maps, coordinates and sub-areas, the client's `world-graph`, zaaps, maps read one by one through `PackCache`, pack format 2 and its layout test, compression measured (D27, D28). | Done on 2026-10-27 |
| 2c | Game tables: items and their types, jobs and skills, interactives, sub-areas' resources, banks and bankers, only the texts they name, pack format 3 at 4.7 MB, the zstd dictionary measured again (D29, D30). | Done on 2026-10-27 |
| 2d | Q4 in code: the host finds the pack of the installed build or downloads it from its release, checks it and does not start without it, the data module in `DefaultModules`, the token from the configuration or the GitHub CLI, `scripts/release.cs` publishing a build's release (D31 to D33). | Done on 2026-10-27 |

## 3. Protocol and connection

Done on 2026-10-28, with decisions D34 to D63. Settles Q2, Q7, Q8 and Q9. Q8 is settled by D34 in session 3a, whose
`hosts` line D35 replaces in session 3b, and whose game connection D52 matches in session 3f; Q7's format by D37, in
session 3b, and its replays with injection by D63, in session 3h; Q2 by D47, in session 3e; Q9 by D60, in session 3h.
Q10, how a build's game is mapped from the previous build's, waits for the next client update, which gets a session of
its own before anything else (D54, D56), and measures again that the client follows the actions D3B sends (D60).

- The mapping pipeline: Cpp2IL dump, scored matching, pinned pairs, anchors from captures, contract audit that fails the
  build (D11), and the IDA tracer if a build needs it (D41). Ideas from BillBot and JondoEmu.
- The readable `.proto` files, the generated classes, the per-build translation, the version gate.
- `D3B.Network`: a Kestrel listener between the official client and the game server, varint framing, codec over
  `IDuplexPipe` (D8, D23).
- Bringing each client's connection to D3B, within D2 (Q8). The client logs in by itself.
- Sending the bot's requests through the client's connection and keeping the client consistent (Q9).
- Recording and replaying sessions. Captures hold an account's session and are never committed: tests replay sessions
  they build themselves.

Eight sessions, the order chosen by the owner. The relay comes before the mapping, so that the client is seen going
through D3B early, the framing is measured on real bytes, and the matching has real captures for its anchors. The
connection phase is mapped before the game, whose anchors need captures of the game connection (D41), and translated
and gated before the game connection is taken, whose server address must be decoded to be rewritten (D46).

| Session | Content | Status |
| --- | --- | --- |
| 3a | Q8: how each client's connection reaches D3B, measured on running clients, and decided: a `hosts` line, and the game server's address rewritten (D34). | Done on 2026-10-27 |
| 3b | `D3B.Network` as a relay: Kestrel on D3B's loopback address and the connection to the server; the client brought to D3B by its own configuration, rewritten by the host while it runs, instead of a `hosts` line; the framing measured on real bytes, varint protobuf in the clear, and `FrameReader` with its benchmark; raw captures, their replay through a `Pipe`, and the tools checking the client and describing captures (D35 to D38). Connection phase only: the game connection still goes straight to the server. | Done on 2026-10-27 |
| 3c | Mapping, part 1: Cpp2IL reads metadata version 39; `scripts/schema.cs` writes each build's obfuscated schema, a `FileDescriptorSet` beside its pack, the structure read from the metadata and every field's encoding from the machine code; `scripts/capture.cs --schema` finds the connection's root message in 3b's captures, all their fields typed (D39, D40). | Done on 2026-10-27 |
| 3d | Mapping, part 2, the connection phase end to end: its readable `.proto` files in `D3B.Protocol` and their generated classes; the matching from pins, anchors measured in 3b's captures, fields and structural scores; the pins of 3.7.12.12, none needed; the contract audit; the mapping, a protobuf message of our own, and `scripts/mapping.cs` writing it beside the build's pack; every readable message, field and value of the connection paired (D41 to D45). | Done on 2026-10-27 |
| 3e | Q2 settled: the readable classes decode the wire once, a build translated only when its mapping holds and keeps the wire's numbers, a frame understood only when it encodes back to its length; the version gate: the protocol module finds the build's schema and mapping beside its pack, else downloads them from its release, audits them, and the host does not start without a mapping that holds; `SchemaFile` shared in `D3B.Protocol`; the release tool publishing the pack, schema and mapping together; the relay reading frames for an observer, which decodes the connection phase and logs it by structure (D46 to D50). | Done on 2026-10-28 |
| 3f | The game connection, measured on running clients: the client takes an address for the game server's host, connects to the first port it is sent whatever its connection port, 443 included, and speaks first, its first frame presenting its ticket; the relay holding the server selection's answer alone until whole, with a way out, and rewriting it once understood whole to D3B's game listener; the game connection matched with its server by its ticket, read by the wire format alone, relayed unread and captured apart from the connection phase; two accounts at once in game through D3B (D51 to D53). | Done on 2026-10-28 |
| 3g | Mapping, part 3: the game's structure measured, most of the first milestone's messages left open by it; pins for what it leaves open, which the mapping tool proposes from captures of the player's actions, build to build left for the next client update (Q10); the readable `.proto` files of the first milestone's game messages, requests whole and events partial, with the `Any` envelope and its type URLs: the game connection, the character choice, the context, the map and its actors and elements, movements, harvesting, the inventory, professions, NPC dialogs and the bank; the game's anchors and the messages its frames carry; mapping format 2; the translation reading game frames through their type URL; no IDA tracer, every field read being measured (D54 to D59). | Done on 2026-10-28 |
| 3h | Q9, measured on the captures then on a client in game through the host: every request of the first milestone carries the id -1, the client counting others alone; D3B sends only the requests that start an action, and the client follows the server's answers as after its own, confirming walks and asking for new maps' information, the player seeing each action; the relay sending the server D3B's frames between the client's, recorded in captures of format 2; the game connection read through the translation by a game session per connection, for the modules observing it; replays with injection, which complete Q7 (D60 to D63). | Done on 2026-10-28 |

## 4. Bot run time and navigation

Done on 2026-10-28, with decisions D64 to D89; D90, taken in session 4f, drops step 7.

- The actor, the state and its snapshots, behaviors, interruption and recovery, reconnection (D9), fed by game
  observers and acting through game sessions (D60, D62); how the bot and the player share the character.
- What the server answers D3B's requests, its refusals named first in the readable protocol (D55, D60).
- Cell pathfinding, world pathfinding over the world graph with doors, zaaps and havens, in `D3B.Engine`; line of sight
  at step 9, since using an element does not depend on it (D75).
- Movement between maps, interactives from the nearest cell the character reaches, the bank (D60).

Six sessions, the split chosen by the owner (D64). A client update before the next session gets Q10's session first
(D54).

Noted by the owner for later, on 2026-10-28: a large effort on criteria, so that what quests, breeds and the server's
codes gate opens to the bot (Q12); what an account without a subscription may do, told apart from what a subscribed one
may, beyond the sub-areas the world path already leaves out (D72), teleporting among it (D83). Then, in session 4e: a
plugin that records in SQLite, as the character enters each map, its NPCs with their ids, questions and replies, and in
a fight the fight's map with its red and blue cells, which the owner believes only the server's fight messages tell
apart; it needs what a plugin sees of the bots (Q11), step 5's storage and step 9's fight messages, the pack's cells
marked red and blue not being measured yet (client.md). A fight has a map of its own, of the pack and off the world
graph (D88).

Left by step 4 for later: why the client at times does not follow a walk, reproduced in session 4f on a map action's
cell that cuts a scroll's walk, about one walk in two there, each costing a journey 3.5 s (D85, D89); a fight lost,
and one an aggression begins during a step, not measured (D88).

| Session | Content | Status |
| --- | --- | --- |
| 4a | The actor and walking on a map: one bot per character of an account, its account known by the ticket the connection phase gave (D65); its inbox fed by an `IGameObserver`, the character's state and its snapshots, behaviors and their expectations (D67); the player winning, the bot pausing until resumed (D66); cell geometry and paths in `D3B.Engine`, measured on the captures (D68); the walk, over once the server answers the client's confirmation, its refusal and the player's stop named, mapping format 3, measured on a client through the host (D69); states compared with snapshots written in the tests (D70). | Done on 2026-10-28 |
| 4b | Between maps: paths through the world in `D3B.Engine`, Dijkstra's over the graph's vertices through scrolls, scrolls with an action and map actions, each change costing the same, their criteria read and answered on three values, strictly, and its benchmark (D71); the account's subscription from its identification, prudently, closing subscribers' sub-areas (D72); changing maps from the cells the client marks, the bot waiting for the new map, what is on it, then half a second for the client to draw it, the server's refusals measured silent so none named, mapping format 3 kept (D73); travelling, the path found again after every change, a failed change left out (D74); measured on a client through the host. | Done on 2026-10-28 |
| 4c | Interactives: the map's own elements in the state, those of the neighbouring maps it also lists left out, and the use the character is in; using an element from the cell the client would, until the use is over, a harvest's end measured; the server's range, line of sight measured not to count, so left at step 9; the refusals named and pinned, mapping format 4 (D75, D78); doors of skill 184 in the path through the world, their criteria strict, what each unknown code would open measured for Q12, no new fact, the owner's choice (D76); every request held until the client drew the map, a use sent too soon measured to stop it confirming walks (D77); measured on a client through the host. | Done on 2026-10-28 |
| 4d | The bank: the map's NPCs, the dialog and the bank's exchange in the state; the banks' insides from the world graph, the nearest one sought, an inside the server shows holding no banker remembered by the host for its bots, the owner's choice (D79); talking to the banker from where the character stands, his reply, the stacks in one request and the kamas in another, leaving, the refusals measured, one named and pinned, mapping format 5, and walking busy during a dialog (D80, D82); the player's leaving told from the client's by the dialog it is sent in (D81); measured on a client through the host. | Done on 2026-10-28 |
| 4e | Zaaps and havens: the player using zaaps, the haven bag and the map measured, and the bot's requests; the zaaps known, the destinations and their costs, teleporting and the haven bag named and pinned, the refusals texts of the error kind, mapping format 6 (D84); the path through the world teleporting from every map that allows it to every zaap known, the cost carried, subscribed accounts only, the haven bag only left, the owner's choice; the zaaps known, a zaap's dialog and the haven bag in the state (D83); the walk the client did not follow measured again, and the state taking the server's cell, the owner's choice (D85); measured on a client through the host. | Done on 2026-10-28 |
| 4f | Activities, interruption, recovery and reconnection: the floors cell paths ignored, measured on the server and counted on maps with stairs (D86); activities of steps chosen from the state, written by modules or plugins, with full pods unloaded before a step, the owner's choice, and a bot's state waited for by others, for step 10's groups (D87); a fight as an interruption, its context named, until step 9 fights it (D88); the recovery, the client's new connection taking the bot back, a walk the server holds let go of, and no walk of no step, which the server closes the connection of (D89); plugins rather than Lua, the owner's choice (D90); measured on a client through the host. | Done on 2026-10-28 |

## 5. Desktop app, first version

Settles Q6 and Q11: both settled in session 5b, by D96 and D95.

- The Avalonia app with ShadUI, which runs the bots in its process (D91): accounts and profiles, the live map with the
  bot's state and its activity, started, stopped and resumed from it (D87), logs and the protocol inspector, in French
  and English (D14).
- Accounts and profiles stored in SQLite; Data Protection once a secret is stored (D92): none is yet (D100).

Five sessions, the split chosen by the owner (D92), after the owner chose the app over the host's gRPC API (D91). A
client update before the next session gets Q10's session first (D54).

Done on 2026-10-29, with 5d last. Next: step 6.

| Session | Content | Status |
| --- | --- | --- |
| 5a | The app with the bots in it: the bots' host in the app's process, its composition shared with `D3B.Host`, which keeps its entry point alone, and no gRPC nor `D3B.Contracts`, the owner's choice (D91); the split of step 5 (D92); the catalogue of the activities modules register, which the app starts (D93); Avalonia 12, ShadUI and Inter, dark by default, the owner's choice, the host's state and why it did not start, the bots page as a card per bot, whole, with its position, pods, kamas, pause and activity, started, stopped and resumed, after the owner turned down three panes, French and English switched live, the window read every 250 ms, and the window drawn in tests (D94); measured on a client through the app. | Done on 2026-10-29 |
| 5b | What a plugin sees and declares: Q11 settled, `D3B.Bot` packed with the libraries it names, which a plugin references, the owner's choice (D95); Q6 settled, a module's page registered with `AddPage`, made by the app from a factory naming no UI type, given the host's services, the app's language, the plugin's own texts and the window's refresh, in a rail on the left, the owner's choice among three mockups, a failing page replaced by its error, and activities and pages named in the app's language (D96); the plugin's root module read from its metadata, so that a plugin with a page loads in a host without the app (D97); the sample plugin's stroll and page, drawn in both languages and themes; the template with an activity and a page, `--page false` without; CI packing the six packages and building both. | Done on 2026-10-29 |
| 5c | Activities' settings, a class its module describes in code, the owner's choice, of every kind and of the game's values, professions, areas, sub-areas and resources, with lists, order, dependencies, conditions, sections and rules, the owner's scope (D98); accounts recorded as the relay sees them, and profiles shared by characters, a character's own preselected and started on connection when asked, the owner's choice (D99); `D3B.Storage`, SQLite through EF Core 10.0.14 and its migrations, shared by the app and `D3B.Host`, applied before the host starts, and no Data Protection, nothing kept being a secret (D100); the rail always shown, the accounts page as a card per account and the profiles page as a column unfolded in place, the owner's choices among mockups, a bot's card offering the profiles, and the language and theme remembered (D101); Lua scripts to replace the external plugins at step 7, the owner's choice (D102); drawn without a screen in both languages and themes. Not measured on a client. | Done on 2026-10-29 |
| 5d | The live map, a page of the rail looking at one bot, shared with the logs and the inspector, a bot's card opening it, the owner's choice among three mockups (D120); what the pack and the state hold, in seven layers toggled, the cells, the character and its path, the elements by what the server says, the NPCs, the exits, map actions and doors from the world graph, the other bots, and the zones and stairs hidden at first, a cell under the pointer described in the app's language, other players and monsters left to steps 9 and 10, the owner's choice (D121); drawn flat from the pack in each theme's palette, no textures, the owner's choice (D122); maps for settings left to the activity that needs them, the owner's choice (D123); drawn without a screen in both languages and themes; measured on a client through the app, the player's walks, a scroll, the haven bag and a fight followed (D124); a fight's placement cells at step 9, from the server, the owner's choice (D125). | Done on 2026-10-29 |
| 5e | Lua scripts measured through the app, a stop, the player's pause and its resume, a failing script, the chunk's name found without `.lua` (D110); the logs and the protocol inspector, two pages of the rail sharing their bot, a bot's card opening its journal, and their filters, the owner's choices among mockups (D111); a bot's journal of its actions, written by the behaviors once over, named from the pack in the app's language, never the protocol's messages, the owner's request (D112); journals, the host's problems and frames kept in memory always, 2,000 entries, 2,000 problems and 3,000 frames, the owner's choice (D113); the host's warnings and errors naming no bot, its own source (D114); the inspector: each bot's frames recorded, D3B's requests included, a frame unfolded in place, the ticket hidden, the shape of what the readable protocol lacks, a capture of the installed build replayed (D115); Lua scripts to go in a session of their own, the owner's choice (D116); drawn without a screen in both languages and themes; measured on a client through the app, a capture of 298 frames replayed, the ticket hidden. | Done on 2026-10-29 |

## 6. Harvesting and banking: first milestone

One account, through its official client and D3B's MITM (D23), harvests a profile's resources across maps and banks
them when its pods are full, controlled from the app (D16). The repository may go public once it is reached (D4).

Three sessions, the split chosen by the owner (D126), a fourth for the app's interface (D143), a fifth for the owner's
harvesting settings (D152) and a sixth publishing the app with its data, then the repository (D158). The milestone was
reached in 6a, on 2026-10-29: harvested from map to map, banked full and back, stopped, paused by the player and resumed,
and a profile started on connection, measured on a client through the app (D133). A client update before the next session
gets Q10's session first (D54).

Done on 2026-10-29, with 6f last. Next: step 9.

- Noted by the owner for later: an option to bank into the guild's chest instead, when the character's guild has one
  and the character the rights; options such as opening resource bags automatically and healing under a share of life
  (D126).
- Auto modes such as BillBot's add what they need to the settings, maps once an activity needs them, the map's control
  reused to pick them (D123), monsters once step 9 puts them in the pack.

| Session | Content | Status |
| --- | --- | --- |
| 6a | The split of step 6, the icons in their own session, the owner's choices (D126); the harvesting module and its activity, its settings of D98's kinds, resources of any profession by priority or everything each character can harvest, an area and its sub-areas, the share of pods and what goes to the bank, D98 grown with sub-areas suggested by resources, percentages and a preview the activity reckons from the pack, and the profile editor as tiles, a zone's mini-map from the atlas, suggested sub-areas, figures, a bar and segments, the owner's choices among mockups (D127); tours through the zone, the wanted resource then the nearest on a map, the nearest map still to go through next, the maps seen empty left out, the host remembering, and the insides last after a measure (D128); banking full at the nearest bank and back, and the reason said when still full (D129); the activity resumed after a fight, a new connection and the player's pause (D130); an activity's report, any activity's, on the bot's card and the map page, with the tour's mini-map and an Activity layer (D131); banking letting the client show its windows, measured (D132); measured on a client through the app (D133); no human timings, D2 (D134). | Done on 2026-10-29 |
| 6b | The icons of the 287 items a harvest names and of the professions (D135), 64 pixels in raw RGBA (D136), decoded from the client's BC7 textures by AssetRipper.TextureDecoder in the extractor (D137); the maps' elements' graphics, what the server says each offers learned by the host from every map, kept in D3B's database, and a tour going to the probable maps first and leaving out those without a resource (D138); pack format 4, its release on the owner's go (D139); Base's resources in 6c (D140); the icons on the tiles, searches, card and map, a search's detail under its name, the owner's asks (D141); measured through the app (D142); a pass on the app's interface in a session of its own, 6d (D143). | Done on 2026-10-29 |
| 6c | What 6a leaves: fights while harvesting, protectors and aggressions; the resources' growing back, measured; harvest statistics; the guild's chest; the owner's options, resource bags and healing, each a request and an event to map; maps reachable only far round in a zone (D133); harvesting Base's resources, wells among them, found by the graphics learned (D140); then the owner's call on making the repository public (D4). Done: a fight resumed, paused or stopped, as the profile says (D144); the growing back learned by the host (D145); statistics in the database and a page (D146); the guild's chest, measured first (D147); bags and healing (D148); isolated maps left out unless taken (D149); Base's resources as the others (D150); the client's display time and every delay in a settings page (D151); the owner's settings in 6e, no random timings (D152); upkeep and the guild's chest measured, mapping 7 (D153); checked through the app, the time between two actions (D154); 6d a redesign (D155); the multiple use, mapping 8 (D156); checked again, good (D157); the repository public once the app ships with its data, in 6f (D158). | Done on 2026-10-29 |
| 6d | A redesign of the app's interface, mockups first, page by page, the profile editor first: less text, one scale of spacing and sizes, ShadUI's components where they fit, the owner listing what to improve (D143, D155). Done: the survey of every page, the editor on a page of its own with its preview beside, one scale for the app, dialogs and toasts, chosen by the owner among mockups (D159); the resources by profession or by list, modelled, made in 6e (D160); Lucide's icons (D161); the sidebar, one bot selector, the map's layers as a menu, texts rewritten short, what a bot meets toasted (D162); the old and the new no longer mixed, one text size, one height, one empty state (D163); checked through the app, good (D164). | Done on 2026-10-29 |
| 6e | The owner's settings of harvesting and journeys: no zaap when the destination is too near, an automatic mode choosing the zone by level, the highest-level resources first, resources to take in or leave out, profiles given by default, and what the owner adds (D152); the resources by profession, left out and ordered, or by list, as D160 models them, in the harvest's card 6d drew. Done: the resources by profession as chips, those left out, the nearest or the highest level first, or a list, the stored profiles read as they were, D98 grown with what an earlier version stored, the professions that gather, a list leaving out, suggestions the activity reckons and a number's unit (D165); the place the activity chooses, the sub-area richest in what each character can harvest at its levels, not above its level, said on the card and for each character in the preview (D166); a zaap only when it saves 5 maps, every journey of the activity (D167); six profiles given, stored once (D168); after 6e, 6f, then fights and their AI, then blueprints (D169); checked through the app, good (D170). | Done on 2026-10-29 |
| 6f | The app published with its data pack, schema and mapping, updating itself from the repository's releases; then the repository public on the owner's go (D158). Done: the app published by CI with Velopack on a tag `v<version>`, self-contained, installed per user in its own folder, unsigned, its notices shipped (D171); the client build's pack, schema and mapping shipped beside it, `scripts/embed.cs` taking them from the build's release, the host looking on this machine then among the app's own and downloading nothing, the token gone, the owner's choice against the lead's (D172); the update checked as the app starts, downloaded, and installed on a restart from the window's banner or on closing, the client the app does not ship said with the update, the settings page saying the version (D173); the repository public at the session's end, each step on the owner's go, the history checked (D174); checked on the owner's machine, installed, updated to 0.1.1 and uninstalled, good (D175); the release v0.1.0 checked, installed from GitHub, good (D176). | Done on 2026-10-29 |

## 7. Mods: Lua scripting, then blueprints

Settles Q13. Lua scripting done on 2026-10-29, in one session, with decisions D103 to D109, then removed (D116, D119).
Settles Q1, settled by D103.

Next, after step 9 (D117, D169): mods in blueprint style, graphs of nodes edited on a canvas in the app, their nodes over D3B's
behaviors and run as activities (D87). Q13 first: who writes them, where they live, what a node is, how a graph runs and
resumes, its settings, and which canvas in Avalonia.

What follows is the Lua scripting this step held first.

Back, the owner's choice (D102), after D90 had dropped it: users extend the bot with Lua scripts, in place of the
external .NET plugins.

- Q1 first, a prototype: LuaCSharp 0.5.8 behind a `BotActivity`, a call awaiting the bot's behaviors, a step cancelled
  by an interruption that `pcall` cannot catch and resumed from the state, a loop that never waits stopped, no access to
  files or processes, errors with their line on the bot's card, and a piece of the owner's miner script ported; the
  owner's 58 scripts compile (D103).
- MoonBot's API among the references; what the owner's scripts call, measured: 37 functions of one family of names,
  routes by map, settings through `dofile` (D104).
- D3B's own API, the owner's choice against the family's names: the activity's hooks given a `bot`, its fields read live
  and its actions answering `ok, why`, `log` and `game` (D105); the readable protocol's requests and events, by name and
  checked (D106).
- A script's settings declared in a table onto D98's kinds, edited in the profiles page, a profile naming a script as an
  activity (D107).
- The scripts of a watched folder, `%LOCALAPPDATA%\D3B\scripts`, listed as activities and read again as they change,
  those that do not read listed with their error; `d3b.lua` and `.luarc.json` written there for VS Code's documentation
  and completion, the owner's request (D108).
- The plugins removed: the loader, the `plugins` folder, the packages, CI's template check, the template, and the sample
  plugin, its stroll written again as `samples/scripts/promenade.lua` (D109).

Measured on a client through the app in session 5e (D110): listed, started, stopped, paused by the player and resumed,
and a failing script. The owner then chose to remove the scripts, in a session of their own (D116), done on 2026-10-29
(D119): `D3B.Scripting`, the scripts' folder and its definitions, the sample stroll and `samples/`, LuaCSharp, and D107's
settings without a class went; modules, activities, their settings and profiles stay, a profile naming a script showing
its activity gone. No activity of D3B's own until step 6 (D118).

## 8. MITM mode

Dropped: MITM is D3B's only mode (D23), built at step 3. The number stays, so that later steps keep theirs.

## 9. Fights

Settles Q5. Before step 7's blueprints, the owner's choice (D169): a clever and complete AI, which thinks again as the
fight changes, handling every case and every criterion of a spell, through effects as data, measured case by case.

Seven sessions, the split chosen by the owner, each layer measured before the next (D177). A session may split in two as
it grows. A client update before a session gets Q10's session first.

- The fight simulator in `D3B.Engine`, from spell effects in the game data, one AI for every breed (D13, D244).
- Every breed's spells through their effects, never per class: the references in `vendor/` give ideas, never code (D3).
- Fights as interruptions of other activities, then fought by them: harvesting fights instead of pausing (D144).

| Session | Content | Status |
| --- | --- | --- |
| 9a | The split of step 9 (D177); the fight's protocol measured on captures the owner made, named and pinned, mapping format 9, and `scripts/capture.cs --messages` to measure it (D178); the fight's state in the bot, its fighters, turns and end, and the journal saying the end (D179); the placement cells, the server's, and the fighters on the app's map, settling D125 (D180). | Done on 2026-10-30 |
| 9b | The fight's data in the pack, format 5: spells and spell levels, their flags read from the client's code, effects, states, monsters and grades with the client's formulas, breeds, characteristics, the Rogue's bombs and walls, spells' variants and pairs; the cast's level and the loot's monsters checked against it; then a release of 3.7.12.12 on the owner's go. Done: format 5 (D182), the flags from the client's code (D183), the checks and the monsters named on the map (D184), the references read again (D181); the pairs the owner meant are variants, the client's pairs its custom mode's, left out; the formulas kept, not run (Q15). | Done on 2026-10-30; the release on the owner's go |
| 9c | The rules of a cast in `D3B.Engine`: line of sight, ranges, zones, target masks, every condition of a spell, cooldowns, moving in a fight with tackle and dodge; checked on captures, D3B accepting and refusing the casts the server did. Done: pure rules on the pack's catalogs (D185), range and line of sight (D186), the 26 zones and the masks, the `HS` criterion (D187), the conditions, costs, cooldowns and casts per turn and target (D188), walking and tackle (D189); measured again on 38 fights the owner played with 11 breeds: 392 of 396 casts and the 298 walks accepted, the 4 others through an Eliotrope's portals, the diagonal and the tackle measured, the spells' modifiers and a carrier's states added (D191); the refusals, which no capture holds, left to captures to make (D190, Q16); the owner's aims for the AI and the fighting activity recorded (D192). | Done on 2026-10-30 |
| 9d | The effects' engine: each effect as data, Bubble's formulas measured one by one (Q5), a bench comparing what D3B foresees with what the server answered, action by action; what the server announces that the fight's state lacks named, mapping format 10, and kept: slides, carries and throws, tackles and their losses, buffs, states and modifiers and their removal (`FightEffectsEvent`), cooldowns (`RefreshCooldownEvent`) (D185, D191). Done: the actions, buffs, cooldowns and shields named and pinned, mapping format 10, and kept in the fight's state with the casts chosen and the moves, the 36 slides and 298 walks starting where it holds their fighter (D193); the critical hit, a cast's field 8 (D194); `D3B.Engine.Effects`, every effect as data, damage, steals, heals, critical hits and the zone's decrease measured, 334 of 335 losses of the casts alone within what D3B foresaw (D195); `scripts/fights.cs`, the bench, and the captures to make for what it cannot settle, pushes, shields, AP and MP removed, erosion, with Q16's (D196). | Done on 2026-10-30 |
| 9e | The AI, playable: placement, then each turn a search for the best sequences of moves and casts, thought again after each answer of the server; the bot fights alone, and harvesting fights instead of pausing (D144); as the owner asks it, autonomous and clever, healing, shielding, moving and choosing its spells and elements by the situation (D192). Done: the owner's asks (D197); a risky fight fought prudently, a lost fight resumed once healed, three in a row stopping, and a switch on the bot's card, the owner's choices (D198); the character's spells, mapping format 11 (D199); `Foresee` in 0.7 µs, a turn planned in 1.6 ms (D200); the AI in `D3B.Engine.Tactics`, an imagined fight, a judge, a beam search weighed against the enemies' reply (D201); the bot placing, closing the fight, playing and ending its turns itself, the player's hand pausing it, harvesting answering its fights once over (D202); 13 fights the owner started, 11 won, and what they taught: kills not spared, no flight, the player's pace, a rejoined fight, a transformed character's spells from the fight's context, the client's preview effects left out, refusals by their error text, the fight's options (D203). | Done on 2026-10-30 |
| 9e2 | The AI cleverer, as the owner asks (D197, D203): measured fight by fight on the owner's, the judge's weights, the enemies' reply beyond the greedy, buffs and states worth what they do, critical hits weighed, line of sight in the threat, positions over two turns, the weapon's blow; the captures of D196 and Q16 the AI's casts make. Done: the owner's asks and choices (D205); the turns and the fights replayed, `scripts/fights.cs --turns` and `--live` (D206); a critical hit weighed by its measured chance (D207); the threat and the approach with the spells that land in sight, the threat's foresight unbiased on 69 fights (D208); buffs and states worth what they do over the rounds they last (D209); the character's next turn searched for the finalists, 3.4% more damage over two turns (D210); the reply searched wider and the weights measured, both kept (D211, D212); the switch kept for each character, the fight played end to end in `TestPlay` (D213); the owner's fights for D196 and Q16 measured, a push's damage settled, 11 casts of breeds' mechanisms the rules refuse (D214); the weapon's blow moved to 9e3 (D215); measured live (D216). | Done on 2026-10-30 |
| 9e3 | The weapon's blow (D215): captures of blows with several kinds of weapon first; the pack's format 6, the weapons' fight data and their type's zone; the character's worn weapon; spell 0 as a level the AI casts, measured blow by blow; then a release of 3.7.12.12 and of the app on the owner's go. Done: swords only, no fight in game, the owner's choices (D217); the 2 blows captured, a sword's, measured, the worn weapon at position 1 (D218); format 6, the 769 weapons and their types' zones (D219); a sword's blow as a level of spell 0, the weapons' formulas the references' (D220); the worn weapon followed, mapping format 12, the bot striking its sword (D221); measured with `scripts/fights.cs`: 8 blows planned on 109 turns that could strike (D222). | Done on 2026-10-30; the releases on the owner's go |
| 9f | The hard cases, one mechanism at a time, each measured, and the other kinds of weapon once blows of theirs are captured (D222): summons, which the bot plays and which play alone as spells and their variants say, such as "Maîtrise des invocations" against "Liberté des invocations", the Steamer's turrets controlled from their third evolution, the Rogue's bombs, the Feca's glyphs, the Huppermage's runes, the Xelor's telefrags, the Zobal's masks, traps and invisibility, portals, carrying, sacrifice, shared damage; the effects on a cell, summons, teleports, glyphs and traps, which 9d does not foresee yet (D195); the spells whose criterion asks a state, such as the Zobal's masks, measured on captures, as the owner asked: 9c's rules answer the criterion, 9d's state gives the states. Done in session 9f, the states, as the server sets and ends them and as the effects set and end them through the spells they cast, the Zobal's masks first and every spell whose criterion asks a state, as the owner asked (D223): the masks measured, the 6 refusals of D203 explained (D224); a buff's end read, mapping format 13 (D225); `C` alone names a caster outside the zone, the Tortoruga loop of D216 gone (D226); the spells effects cast imagined, a spell's buffs dispelled and replaced (D227); the judge reading the criteria on states, the AI changing masks for the spells it casts (D228); measured with `scripts/fights.cs`, 111 of 244 states foreseen against 24 (D229). Left, after the fights' profiles (D230): summons, the bombs, glyphs, runes, telefrags, traps and invisibility, portals, carrying, sacrifice, shared damage, the other kinds of weapon. | States done on 2026-10-30; the other mechanisms after the fights' profiles (D230) |
| 9f2 | Summons, those the character plays and those that play alone, as their spells say, measured on captures, imagined, searched by the AI and measured with `scripts/fights.cs`; then what 9g6 left (D263, D266). Done: the owner's asks, the references read (D268); the 57 summons and 20 controlled turns of the captures measured, their characteristics, turns and control, "Maîtrise des invocations" read in the pack (D269); summons brought with those characteristics, judged by what they deal and the blows they draw, striking first in the reply, 71 planned against 9 before and the players' 42 (D270); the bot playing the summons the client controls (D271); the heals and critical damage out of fights measured to change nothing, not taken (D272); a fight begun between two steps answered by the activity, D266's protector counted (D273). Left: the owner's captures of summons, an Osamodas's and "Maîtrise des invocations" on and off, and the AI's summons measured in game; a summon replacing another, doubles and illusions; with the owner in game, an aggression, stairs and portals, the journey after a refused teleport, the inactivity popup. | Done on 2026-11-02, not measured in game |
| 9f3 | The owner's captures of summons measured with `scripts/fights.cs`, then the Roublard's bombs, their explosion, walls and limit, measured, imagined, searched by the AI and measured; with the owner in game if it comes (D268, D273). Done: the owner's asks, the references read, every breed's mechanisms listed by the owner, each to take in through every move that brings fighters into it (D274); every fight's placement cells kept in the database with the map fought on, another than the map left in 107 of 163 fights, and the one left, its area, sub-area and coordinates, each set apart, the owner's ask and columns (D275); the bombs measured on the 5 fights of a Roublard, their chains, combos summed, walls, slots, a bomb on a fighter and the Roublabot's push to a cell (D276); set off with their chain, walls struck through every move, slots and their worth in the AI, the Détonateurs' targets as imagined, the AI planning 31 bombs on 32 turns against the player's 17 (D277). Left: the owner's captures of summons and of the fight map's preview (`FightMapInformationResponse`), not made; walls at a turn's start and walks across them; the bombs' starting spell; the pushes the server stops short, every breed's; the measures in game. | Done on 2026-11-02, not measured in game |
| 9f4 | The owner's captures measured, an Osamodas's summons, a Roublard's fights the AI plays and the fight map's preview; what 9f3 left; then the Féca's glyphs, measured, imagined through every move, searched by the AI and measured; with the owner in game if it comes (D274, D277). Done: the owner's asks, then the Roublard measured whole before another breed, as the owner asked once it played badly in game, few walls and turns passed doing nothing (D278); a summon's spells list kept apart from its character's, whose Roublabot's had taken its Roublard's place, 21 turns passed, and the marks on the cells read, mapping format 15, `FightMapInformationResponse` found in the schema, not mapped (D279); glyphs and bombs' walls struck as a turn begins and ends, measured on monsters' glyphs, imagined, in the reply and the judge, the Féca's glyphs among them (D280); the bombs' promise on an enemy counted once with the approach, the Détonateur that wins played (D281); a bomb's starting spell, an AP more on its kind's spell while it lives (D282); pushes and pulls as the server moves them, 138 of 154 pushes and 79 of 86 pulls on the cell imagined against 116 and 57 (D283); measured, the Roublard's plans empty 13 then 0, kills foreseen 40 then 77 (D284); the owner's second capture: a monster moved from wall to wall not struck again, a Botte harmless, the search kept as it was, a wider one costing more than it finds (D285); walls and glyphs to weigh over the rounds they strike, the owner's observation that walls pay more against big enemies (D286). Left: the Roublard measured in game with `Fights:Alone`, the owner's ask before another breed; the Féca's glyphs measured in game, an Osamodas's summons and "Maîtrise des invocations", the fight map's preview, none captured; a glyph-aura's buffs taken back as a fighter leaves it; a Pulsar's pushes the server does not make; enemies walking through a wall; with the owner in game, D273, an aggression, stairs and portals, the inactivity popup. | Done on 2026-11-02, not measured in game |
| 9g | Strategies: the profile's settings, a role, prudence, spells preferred or left out, the fight closed and spectators blocked or not (D203), and the AI tried on many fights, per breed through the effects; then the fighting activity, made as the harvest is: the monster, zone and sub-area, the group's least and most monsters, a drop sought (D192). Its profiles first, before 9f's other mechanisms, the owner's choice (D230): made as the harvest's, with many useful options, and the AI fighting a harvest's fights a harvest profile's option, checked by default, instead of the switch on the bot's card. Done in session 9g, as the owner asked, the fighting activity in the same session (D232): a strategy each character plays, a profile of its own (D233), its options (D234), the fights out of an activity its option, the switch gone (D235); the breeds' spells' icons, then the monsters' icons and where they live, pack format 7 (D237, D238); the monster groups on a map, mapping format 14, Q15 settled (D239); the strategies made (D240); the fighting activity, its options (D236) and made (D241); measured with `scripts/fights.cs`, nothing changed with Standard (D242). Left: the fighting activity measured in game, a sub-area suggested by the monsters chosen, isolated maps, aggressive monsters and a drop sought (D241). The strategies removed in 9g2 (D244). | Done on 2026-10-30; measured in game and the releases on the owner's go |
| 9g2 | The fighting activity measured in game on the owner's characters, the attacks, the walks made again, the groups judged winnable against the fights' outcomes, the place, the healing and the bank, and what the game shows fixed; what D241 left that serves (D243). Done: the strategies removed, one AI for every character, the owner's choice (D244); the sub-areas suggested by the monsters chosen, isolated maps, and the place chosen among sub-areas of maps shown, measured on the pack (D245); the monsters' picker, the fight profile's preview and the editor in a narrow window (D246); the loot's icons in the pack, families of monsters (D247); every interactive transition followed, refused teleports remembered, the ways through an NPC left (D248); groups judged over the fight's course (D249); measured in game, 22 attacks and 22 fights won on five breeds (D250). Left: the ways through an NPC, Incarnam's first; the Zobal's masks; every item's icon (D250). | Done on 2026-10-30; the releases on the owner's go |
| 9g3 | The ways through an NPC's dialog, Incarnam's portal first, measured on a capture the owner made by hand, BillBot read, and the client's data searched for what knows these NPCs without a text in the code; then what 0.3.x had not measured in game (D248 to D252). Done: the client's data holds no reply's action nor destination, so a way is measured and known by the NPC's and replies' ids, the pathfinder and the map change going through it, a way refused remembered, and the client's own leaving of a question without reply no longer pausing the bot; Incarnam left by the bot through the portal, 77 maps reached from there before, 530 or 5,420 now (D253); measured in game, what is left (D254). Left: the other ways through an NPC, each measured in game; the interactive transitions other than doors, a teleport refused, big groups, Sarakech's bank and the inactivity popup, not measured in game (D254). | Done on 2026-11-01; the release on the owner's go |
| 9g4 | The Zobal's masks, which the AI put on at its turn's end for the next turn rather than first to cast their spells (D250), measured with `scripts/fights.cs --turns` and corrected in `D3B.Engine.Tactics`; what D254 left, measured in game. Done: the turn's search keeps, beside its best sequences, the best of each set of states the character holds, so that a mask lives to the spells it opens: on the Zobal's 36 turns, the mask put on first 26 times against 13 and last 5 against 14, the plans judged 29,149 against 26,260 (D255); not measured in game, the owner unable to capture; what the owner met in game, the hunt passing groups on the maps a journey crosses, food taken from the inventory, healing that waits too long and harvesting that leaves resources, left to the next session (D256). Left: the measures in game of D254 and D255. | Done on 2026-11-01, not measured in game |
| 9g5 | What the owner met in game (D256), each case measured on captures before it was corrected, then the measures in game D254 and D255 left. Done: the food the inventory holds eaten when the profile chose none, by the life an item's use gives back when that is all it does, pack format 8 (D257); without food, the regeneration back to the share alone (D258); a hunt's journey stopping on the first map of its zone holding a group it attacks (D259); the stops of 10 to 20 s, walks the client does not follow as they end where its previous walk did, avoided (D260); measured in game, the Zobal's masks right, a big group lost before the first fight (D261); harvesting measured in game, nothing left on the maps, and an element another harvested growing back from when a bot first saw it so (D262). Left: the interactive transitions other than doors, the journey after a teleport refused (D262), Sarakech's bank and the inactivity popup, not met in game (D261); the first fight's judge, by levels alone. | Done on 2026-11-02 |
| 9g6 | What 9g5 left (D261, D262): the hunt's first fight judged before any fight from the characteristics the server gives, measured on the captures; aggressive monsters measured on captures with the owner in game; the miner's automatic place that led into a mine without ore, measured on the pack and the graphics learned; every item's icon at the size the owner chooses; with the owner in game, the transitions other than doors, the journey after a refused teleport, Sarakech's bank and the inactivity popup. Done: the characteristics out of fights equal the first turn's, where the placement's fighter the judge took lacks the elements', so a group is judged from them, before the first fight too, the breed from the spells listed, none judged by levels on the captures and 9g5's group of 8 turned away (D263); the harvest's automatic place counting no isolated map the tour leaves out, the Souterrains, 12 undergrounds apart, no longer first for a miner (D264); every item's icon at 32 pixels, the owner's choice, pack format 8 still, 26.9 MB (D265); measured in game with three profiles the lead made, Sarakech reached only by its zaap, so the boat from the Port de Madrestam one way more, Sarakech's bank found in its third inside, two groups judged winnable lost (D266); after a lost fight, a group no less risky turned away for the run, the owner's choice (D267). Left: an aggression, none met and the pack saying nothing of which groups attack; the transitions other than doors, the journey after a refused teleport and the inactivity popup, not met. | Done on 2026-11-02; the release on the owner's go |

## 10. Several accounts and groups

- Many accounts in one host, each through its own official client (D23), a group with a leader and followers.

## 11. Marketplace and crafting

- Reading prices, selling, collecting sales, crafting with recipe checks.

## 12. Treasure hunts and dungeons

- Clue solving, hint NPCs, dungeon routes.

## 13. The owner's profession scripts

- The scripts in `vendor/metiers` written again for D3B, what they call measured in D104: their routes by map, harvests
  and banking run as activities (D87), and tested end to end. How, the Lua API gone (D116), is decided at that step,
  after step 7's blueprints (D117).
