# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What we are building

**Social Tennis**: an offline-first iPhone PWA (Blazor WebAssembly, no backend) that schedules social tennis doubles sessions. The product is defined in the docs below. Read them rather than relying on this file's summary.

## Governing documents (read before working)

All live in `app.claude.tennis/docs/`:

- `spec.md`: **what** the finished product does, including exact UI wording. Quoted text and **bold** labels are literal strings the app must use.
- `codingStandard.md`: C# style, nullability, async, DI, error handling, testing libraries, open-source-only packages.
- `projectStandards.md`: **how well** it must be built and verified: solution layout, architecture, offline/PWA, UI, analyzers, testing, quality gates, versioning, source control.
- `taskN.md` (`task1.md` … `task6.md`): the increments of work, done in order.

Rules for working with them:

- Work in task order. Read `spec.md` and the current `taskN.md` first and implement the task as written. `spec.md` describes the end state, so an early task may describe something a later task changes.
- **Never edit `spec.md` or the task files.** They are inputs. If one seems wrong or ambiguous, choose the simplest reading and record it under "Decisions" below.
- A task is done only when it meets the Definition of Done in `projectStandards.md` section 7: tests for every requirement, zero warnings and errors (Debug and Release, every project, IDE as well as CLI), full suite passing including browser tests, and this file updated.

## Key standards at a glance

(Summary only; the standards files are authoritative.)

- .NET 10, C# 14, nullable and implicit usings on, file-scoped namespaces, records, primary constructors, collection expressions. No `!` without a documented reason. Async all the way (never block; Blazor WASM is single-threaded).
- Everything must be open source (OSI licence, checked for the version used). Keep packages minimal.
- Logic (rules, validation, parsing, schedule engine) is plain .NET, separate from UI, and testable without a browser. Pages and components stay thin. Storage sits behind an interface. Randomness is seedable.
- Saved data must stay readable by later versions. Multi-step flows save after every step. Release publish is trimmed, so serialisation must work trimmed.
- Mobile first, portrait, iPhone SE (375×667) minimum, no horizontal scroll. Centred dialogs that fit the screen. Light and dark themes, 4.5:1 contrast, accessible names on all controls.
- `.editorconfig` plus StyleCop apply to every project. Warnings are errors in Release. Fix analyzer warnings, never suppress them.
- Tests: NUnit (constraint assertions), NSubstitute only at true boundaries, Method_Scenario_Expected names, Arrange-Act-Assert. Browser tests use WebKit at iPhone sizes against the published Release build, from empty storage, runnable with one command.
- Source control: `master` is the release branch (every push deploys to GitHub Pages), `develop` is integration, each task on `feature/<name>` (e.g. `feature/task5`) cut from `develop`. Short imperative commit subjects naming the task. Never commit build or test output.

## Repository layout

- Repo root: `SocialTennis.slnx` (every project), `Directory.Build.props` (shared build settings and StyleCop), `.editorconfig`, `stylecop.json`, `scripts/` (`publish.sh`, `set-base.sh`, `stamp-version.sh`), `.github/workflows/deploy.yml`, `README.md`, `.gitignore` and this file.
- `app.claude.tennis/`: the .NET 10 Blazor WebAssembly PWA (root namespace `SocialTennis`).
  - `Navigation/`: plain logic for the toolbar items and header titles (`AppNavigation`) and `AppStart` (opening on Play with nothing in play goes to Players, once).
  - `Layout/`: `MainLayout` (header, content, toolbar), `BottomToolbar`, `ToolbarIcon`, `ConfirmDialog` (centred confirmation), `ChoiceDialog` (centred dialog with any buttons: press-and-hold options, already played together), `RoundActionDialog` (Start / Finish / Cancel round), `ImportDialog` (paste players, red squiggles under mistakes), `PlayerPicker` (search box, suggestions and Add, shared by the Players step and Review's edit mode), `CourtBall` (SVG tennis ball with the court number), and the schedule components `CourtsStep`, `PlayersStep`, `ScheduleReview` (edit mode), `ScheduleCards` (round cards, shared by Review and Play), `PlayerLabel` (rank dot, pin icon, red/blue `PlayerMark`).
  - `Players/`: plain logic: `Player`, `PlayerValidator`, `PlayerList` (search and sort), `PlayerService` (storage), `PlayerJsonContext`, `PlayerImport` (parse the Import text; `ImportError`, `ImportedPlayer`, `ImportResult`), `PlayerMerge` (skip players already there), `PlayerBackup` (the backup file; `PlayerBackupFile`, `BackupReadResult`), `PlayerMessages` (import, backup and restore sentences).
- `Settings/`: `VersionInfo` and `VersionService` (read `version.json`), `ThemeChoice` and `ThemeService` (Light, Dark, Device).
  - `Scheduling/`: plain logic: `Schedule` (and `Round`, `Game`, `SchedulePlayer`, `ScheduleStep`), `ScheduleEngine` (the schedule rules; `Continue` builds the rounds after those already played), `ScheduleRoster` (add or remove a player part-way through), `ScheduleFlow` (create steps, play, reorder), `RoundProgress` (start, finish, cancel a round; `RoundStatus`), `ScheduleEditor` (pin, swap, repeat partners, Re-schedule; with `PlayerSpot`, `PlayerPair`, `PlayerOptions`, `EditResult`, `RepeatPair`, `PlayedTogether`), `CourtList` (parse courts), `PlayerNames` (short names), `ScheduleService` (storage), `ScheduleStorageJsonContext`.
  - `Storage/`: `IKeyValueStorage` and `LocalStorage` (browser localStorage via JS interop), `BackupFiles` (the backup file in the browser, via `js/backup.js`).
  - `Pages/`: thin routed pages (Play `/`, Players `/players`, PlayerEdit `/players/add` and `/players/edit/{id}`, SchedulePage `/schedule`, About `/about`, NotFound).
  - `wwwroot/`: `index.html`, `manifest.webmanifest`, icons, `js/pwa.js`, `js/reorder.js` (drag to reorder), `js/hold.js` (press and hold a name), `js/backup.js` (save and restore the players backup), `version.json` (the version the About page shows; the deploy rewrites it), `service-worker.js` (dev, no-op) and `service-worker.published.js` (offline cache, used by publish).
  - `icon-source/icon.svg`: source of the PNG icons (rendered by hand with headless Chrome).
  - `docs/`: the governing documents.
- `app.claude.tennis.Tests/`: NUnit unit tests (logic only, no browser).
- `app.claude.tennis.BrowserTests/`: NUnit + Playwright WebKit tests against the published Release build.

## Commands

Run from the repo root:

- Run (dev): `dotnet run --project app.claude.tennis` serves http://localhost:3000.
- Build everything: `dotnet build` (and `dotnet build -c Release`; Release treats warnings as errors).
- All tests, unit and browser: `dotnet test`. The browser tests publish the Release build with `scripts/publish.sh`, serve it from a local static server (at `/` and at the `/tennis/` subpath), and drive WebKit at iPhone SE size. The first run downloads WebKit automatically. Takes a few minutes because of the trimmed publish.
- Unit tests only: `dotnet test app.claude.tennis.Tests`.
- Publish for GitHub Pages: `scripts/publish.sh <output-dir> /<repo-name>/` writes the site to `<output-dir>/site` (rewrites `<base href>`, adds `404.html` for deep links and `.nojekyll`). With `APP_VERSION`, `APP_COMMIT` and `APP_RELEASED` (yyyy-MM-dd) set, it also runs `scripts/stamp-version.sh`, which writes `version.json` for the About page; the deploy workflow sets them (`1.0.<run number>`, the short commit, today's date). Without them the site keeps the development version and About says "Local build".

## Architecture (current state)

- Entry point `Program.cs` mounts `App` on `#app`. `App.razor` routes with `MainLayout`; `NotFound` is the not-found page.
- `MainLayout` shows an `h1` header with the current page label (from `AppNavigation.TitleFor`, updated on `LocationChanged`), the page in a scrolling `main`, and `BottomToolbar` (four `NavLink`s with SVG icons) at the bottom. Safe-area insets are respected. Light and dark colours are CSS variables in `wwwroot/css/app.css` (follows the device; a `data-theme` override is ready for the About page task).
- Offline: `service-worker.published.js` precaches every file in the generated `service-worker-assets.js` manifest (no integrity checks, see Decisions), answers navigations with the cached `index.html`, deletes older caches on activate, and takes over at once (`skipWaiting` + `clients.claim`). `js/pwa.js` registers it and reloads once when a new version takes control.
- Players (task 2): `Players.razor` lists, searches and sorts; `PlayerEdit.razor` adds, edits and deletes (with `ConfirmDialog`). Players are one JSON list in localStorage (`socialTennis.players`) and the sort choice is stored as `socialTennis.playerSort`; `PlayerService` is the only code that touches them. Serialisation is source-generated for trimming.
- Schedule (task 3): one schedule in localStorage (`socialTennis.schedule`), saved after every step and resumed at the same `ScheduleStep` (Courts, Players, Review, InPlay). `SchedulePage` switches between `CourtsStep`, `PlayersStep` and `ScheduleReview`. `ScheduleEngine.Generate(players, courtCount, seed)` is a seeded randomised search; the Play page shows an in-play schedule read only with court numbers. Drag to reorder uses pointer events in `js/reorder.js`, which only reports the move back to `ScheduleCards`.
- Editing (task 4): the Edit button on Review turns on edit mode (`ScheduleReview` state, not saved). Names become buttons; `js/hold.js` reports a 500 ms press-and-hold (or Enter on a focused name) to `ScheduleCards`, which opens a `ChoiceDialog` with the options from `ScheduleEditor.OptionsFor`. Pins (`Round.Pinned`), blue markers (`Round.Changed`) and kept-together pairs (`Schedule.AcceptedPairs`) are saved with the schedule; red is worked out from the rounds (`ScheduleEditor.RepeatPairs`). Re-schedule is a greedy search of swaps within a round that must reduce repeat partners without worsening fairness, opponent excess or rank clashes. Done and Play clear the blue markers.
- Play (task 5): each `Round` has a saved `IsStarted` flag; `Round.Status` is Complete (all games complete), InPlay (started) or NotStarted. `ScheduleCards` with `ShowCourts` draws a `CourtBall` per game (blue/green/grey fills, white bold number with dark outline, seams at the edges), the status text and a ⋮ button per round header; `Play.razor` opens `RoundActionDialog` and saves through `RoundProgress`.
- Clean-up (task 6): `Schedule.IsEditing` is saved, so a new schedule stays in edit mode until Done or Play. In edit mode Review shows a `PlayerPicker` ("Add a player to the schedule") and the hold dialog offers Remove from schedule (a `ConfirmDialog`); both go through `ScheduleRoster`, which builds the open rounds again with `ScheduleEngine.Continue`. Play has Close schedule (a `ConfirmDialog`, then `ScheduleService.DeleteAsync`) and no Edit link. `MainLayout` tells `AppStart` the address the app opened on and `Play` sends the user to Players once if nothing is in play. Players has Import (`ImportDialog`, `PlayerImport`) and a Backup section (`PlayerBackup`, `BackupFiles`, `js/backup.js`). About shows `version.json`, the Theme choice (`ThemeService`; `index.html` applies the stored choice before the page is drawn) and How to use.

## Zero diagnostics rule

**No errors, warnings or messages.** That includes IDE-only suggestions and hints, not just what `dotnet build` prints. Before calling any work done:

- `dotnet build` and `dotnet build -c Release` show 0 warnings and 0 errors.
- `dotnet format style SocialTennis.slnx --verify-no-changes --severity info` and `dotnet format analyzers SocialTennis.slnx --verify-no-changes --severity info` report nothing.
- The IDE diagnostics (VS Code Problems) for the repo's files are empty. Stale entries for the unrelated `Claude.Tennis.App` solution outside the repo are not ours.

Fix the cause; never suppress.

## Maintaining this file

Update this file as part of finishing each task, not afterwards:

- Keep "Repository layout", "Commands" and "Architecture" matching the code (projects, folders, how to build, test, run browser tests, publish).
- Record each decision where the spec or task left a choice open, and each new package or abstraction with its licence and reason (required by `codingStandard.md`).
- Don't copy rules from the standards files here. Link to them so there is one source of truth.

## Task progress

- [x] task1
- [x] task2
- [x] task3
- [x] task4
- [x] task5
- [x] task6

## Decisions

### Task 1

- **Packages** (all open source): `StyleCop.Analyzers` 1.2.0-beta.556 (MIT; the beta is used because 1.1.118 misreports target-typed `new` and record parameters), `NUnit` 4.5.1 (MIT), `NUnit3TestAdapter` 6.2.0 (MIT), `NUnit.Analyzers` 4.12.0 (MIT), `Microsoft.NET.Test.Sdk` (MIT), `Microsoft.Playwright` 1.63.0 (Apache-2.0). Bootstrap and the template's sample pages were removed.
- **Repo-wide analyzer choices** are in `.editorconfig`, each with its reason beside it (for example SA1101/SA1309 are off because the coding standard forbids `this.` and wants `_camelCase` fields).
- **Home page:** Play is `/`. The spec's "go to Players when nothing is in play" depends on schedules, so it arrives in a later task.
- **Placeholder pages:** Players, Schedule, About and Play show a one-line placeholder until their tasks.
- **Unknown address:** the header reads "Not found" and the page shows the spec's message; the toolbar stays.
- **Service worker has no integrity check** because deploy rewrites `<base href>` in `index.html` after the build, which would fail a hash check. Files are fetched with `cache: 'no-cache'` instead.
- **Updates:** the new service worker activates immediately and the page reloads once, so installed copies pick up a new version the next time they open online. Data is not touched by this.
- **Deploy:** `.github/workflows/deploy.yml` runs all tests on push to `master`, then publishes with `scripts/publish.sh` under `/<repo-name>/` and deploys to GitHub Pages. Version stamping and the About page version arrive with the About task.
- **Test projects** are named `app.claude.tennis.Tests` and `app.claude.tennis.BrowserTests` with namespaces `SocialTennis.Tests` and `SocialTennis.BrowserTests`.
- **Not applicable yet:** dialog-fit and stamped-version browser tests (no dialogs or About version exist until later tasks).

### Task 2

- **Scope:** add, edit, delete, list, search and sort players, kept on the phone. Import and Backup/restore from `spec.md` are not in `task2.md`, so they are left for later.
- **Spec wins where `task2.md` is looser:** last name is optional, the spec's validation messages, search wording and sort labels are used, and the sort drop-down is labelled "Sort by".
- **Storage:** plain localStorage through `IKeyValueStorage` (no package). Unreadable stored data loads as an empty list rather than stopping the app. Ranks above 3 load as 3. Unknown or missing JSON fields are tolerated so later versions can add fields.
- **Sorting** uses ordinal, case-insensitive comparison so results do not depend on the browser's culture data.
- **Validation** runs when Save is pressed (no `maxlength` on the inputs, so the spec's length messages can show).
- **Branch:** `feature/task1` was not yet merged into `develop`, so task 2 work sits on top of it.

### Task 3

- **Page component name:** the routed page is `SchedulePage` (route `/schedule`) because `Schedule` is the model type.
- **Engine:** per round, the players with the fewest turns at the "special" role (sitting out, or the three-player game, or singles; only one of these exists in a given session shape) take it, which keeps shares within one of each other. Doubles are then found by a depth-first search with a node budget, and whole attempts are repeated. Phases: opponents at most twice; then at most three times; then (singles only, and only if ranks 1 and 3 are both present) a rank 1 may play a rank 3, preferring fewer such games. 4 and 5 players give 3 and 5 rounds; otherwise 6.
- **Opponent limit:** the spec's "at most twice" is not reached for 6 players with singles (the spec's own example) nor for 7 players with a three-player game (not found by a long search); both fall back to three. Tests allow three for those two sizes with two or more courts.
- **Fair shares:** a player's three-player count includes both the lone player and the pair; which role they get is only a soft preference.
- **Court order:** courts are saved ascending; game N is on the Nth lowest court. The odd game (three-player or singles) is placed last in the round, so it takes the highest court used.
- **Names on cards** are first name plus last initial everywhere (the spec's card rule), so four names fit a row on an iPhone SE. Rank dots show on Review only; a singles game of rank 1 against rank 3 is shown in red.
- **Edit button on Review** was shown disabled in task 3 (task 4 turns it on). The Play page has an Edit link to Review (as the task asks), and Review hides the "‹ Players" link once the schedule is in play. (Task 6 removed the Edit link from Play.)
- **Courts input:** blank entries from stray commas are ignored (`4,5,` is courts 4 and 5); a negative number gets "Court numbers must be 1 or more."
- **Players step:** "Add" needs a suggestion to be picked first (as the spec says). Each add/remove is saved. Players are copied into the schedule, and names and ranks are refreshed from the player list when the schedule is generated.
- **Generating** waits 50 ms before the (blocking) search so "Matching players…" can paint; the seed comes from `Random.Shared` in the page and is stored in the schedule.
- **Layout fix:** `#app.loading` stays a centring grid after load, which let a tall page grow the shell past the screen; it now has a one-row grid.
- **Not done (later tasks):** Edit mode, pins, swaps, Re-schedule, round start/finish, Close schedule, and sending the user to Players when nothing is in play.
- No new packages.

### Task 4

- **Scope:** pin, unpin, swap, red and blue markers, Re-schedule and the already-played-together dialog, with the Edit/Done toggle on Review. Remove from schedule and Add a player are in `spec.md` but not in `task4.md` (they arrive with the later clean-up task), so the options dialog offers only Pin, Unpin and Swap. Nothing yet marks a game complete, so completed games are covered by unit tests and by seeded schedules in the browser tests; the Play page's round actions arrive in task 5.
- **No pin confirmation:** `task4.md` mentions a "pin confirmation" but `spec.md` says "Pinning needs no confirmation", so Pin acts at once and the banner says "Pinned <name>."
- **Pins and markers live on the round** (`Round.Pinned`, `Round.Changed`), so they move with a round when it is dragged. A pin keeps the player in their round's game; Re-schedule and Swap refuse to move them. Older saved schedules load with none.
- **Edit mode is not saved** (only the pins, markers and kept pairs are). The newly-created-schedule-opens-in-edit-mode rule is in `task6.md`.
- **Red and blue** show on Review (always) and never on Play; pin icons show on both. Colours are never the only signal: red and blue names carry hidden text "(partners again)" and "(moved)".
- **Messages the spec leaves open:** "Pinned <name>.", "Unpinned <name>.", "Swapped <a> and <b>." (plus " Partners who play together twice are shown in red." when that happens), "<a> and <b> are both sitting out.", "Keeping <a> & <b> together." Messages that end in a short name's initial do not get a second full stop.
- **Re-schedule rules:** a swap must reduce repeat partners and must not make fairness (sit-outs, three-player games, singles within one of each other), opponent excess (over two, then over three) or 1-v-3 singles worse. Among those moves it prefers rounds the user has not touched, then fewer extra opponent repeats. Repeats that involve a completed game are left for the user's choice in the dialog; if only those remain the message is "Some partners already played together in a completed round: choose who should move."
- **Press and hold** is a pointer timer in `js/hold.js` (500 ms, cancelled by moving more than 10 px or releasing). A hold swallows the click that follows. Enter on a focused name does the same for keyboards and is how most browser tests open the dialog without waiting; one test uses a real hold.
- **Dialogs** use the existing centred `.dialog` (scrolls inside itself, clear of the safe areas); `ChoiceDialog` stacks its buttons.
- No new packages.

### Task 5

- **Scope:** ball, row layout, round actions and dialog on the Play page. Close schedule and the Play-page messages beyond what exists are left for task 6; the Edit link on Play is unchanged.
- **Round state:** "in play" is the saved `Round.IsStarted` flag (older saved schedules load as not started). Finish marks every game complete and clears the flag; Cancel only clears it. `RoundProgress` refuses to start a complete round or a second round.
- **Ball:** an inline SVG (`CourtBall`), not an image, so it themes and needs no cache entry. Fills are blue `#1558b0`, green `#2a7a2e`, grey `#5b6166` (all above 4.5:1 against white; checked in a browser test). Two-digit numbers are squeezed with SVG `textLength` rather than shrunk so they stay tall and clear of the seams.
- **Row layout:** `ball | side | v | side | spacer` with the spacer as wide as the ball, so the v is the centre of the row and each side is centred in its half. Rows without a ball (Review) are unchanged.
- **Accessible names:** the ball is `role="img"` named "Court n"; the header button is named "Round n actions" and shows ⋮; the status text (In play / Complete) is shown only for those two states.
- **Dialog:** reuses `ChoiceDialog` (already centred, scrolls inside itself and clears the safe areas); browser tests check it on the iPhone SE profile scrolled to the bottom and in a very short window.
- No new packages.

### Task 6

- **Scope and branch:** `task6.md` is a general clean-up and lists several things earlier tasks already did (rank None and 1–3, pin and unpin, Play naming, the singles rules, Re-schedule fixing the round the user did not edit). Those were checked against the code and their tests, not rebuilt. The branch is `feature/task6` (the task file's `feature/ui_clean_up1` is not used, per the source-control standard).
- **Menu:** the task lists a Timer item and then says to remove it; no Timer page ever existed, so the menu stays Play, Players, Schedule, About (as `spec.md` says).
- **Start page:** `AppStart` records whether the app opened on the Play address. Only then does Play, with nothing in play (no schedule, or one still being set up), replace the address with Players. Opening on any other page, or tapping Play later, never redirects.
- **Edit mode is saved** as `Schedule.IsEditing` (`task4.md` kept it unsaved; `spec.md` and `task6.md` want a new schedule to stay in it after the app is closed). `ScheduleFlow.Generate` turns it on; Done, Play and going back to Players turn it off; Done and Play also clear the blue markers. Older saved schedules load as not editing.
- **Add and remove a player** rebuild every round that is not complete with `ScheduleEngine.Continue`, which replays the completed rounds first (partners, opponents, shares) so nobody partners twice if that is possible and sitting out, three-player games and singles stay shared. A player who was not in a completed round counts it as sitting out, so late arrivals play. If no schedule without a repeat partner exists, the search ends in a last-resort phase that allows repeats, which Re-schedule then tries to fix. Pinned players are put back in their old game, side and position when the new round has that place (the pin is kept either way); if repeats are still left and any pin exists, the message adds the spec's "pinned players are in the way" sentence. Blue marks only the added player. The random seed comes from `Random.Shared` in the page.
- **Who is "in the schedule":** `ScheduleRoster.ActivePlayers`, meaning the players in a round that can still change (or in any round when all are complete). A removed player who stays in `Schedule.Players` for the completed rounds can be added back. The four-player minimum counts active players.
- **Remove from schedule** is offered wherever Pin, Unpin or Swap is, and never for a player in a completed game or round. If the confirmation is accepted with every round complete (not reachable from the dialog), nothing changes.
- **Version stamp:** `wwwroot/version.json` holds the development version and is rewritten after the build by `scripts/stamp-version.sh` (like `<base href>`, so the offline cache is unaffected: the service worker has no integrity check and is in the precache manifest). The script also appends the version to `service-worker.js` as a comment, because a service worker only updates when its own bytes change, and a deploy that changed only the version would otherwise never reach installed copies. The browser tests publish with a known stamp (1.0.42, abc1234, 7 March 2026).
- **Theme:** stored under `socialTennis.theme` as `light`, `dark` or `device` (the default, also used for anything unknown). An inline script in `index.html` sets `data-theme` before the stylesheet is used; `socialTennis.applyTheme` in `pwa.js` changes it live.
- **Import syntax:** blank lines are ignored; a missing last name after the comma (`Sam,`) and an empty rank (`Sam, Lee,`) are allowed; a single value is an error. A line with a quoting mistake reports only that mistake; otherwise every problem on the line is listed. The first five errors are listed. The squiggles are drawn by a copy of the text behind a transparent text box (`.import-mirror`), kept scrolled with `socialTennis.linkScroll`. The same name twice in one import counts as "already here".
- **Backup file:** JSON `{ "App": "social-tennis", "Version": 1, "Players": [...] }` (property names are case-insensitive when reading), at most 2,000,000 characters. Restoring skips players with the same id or the same first and last name, keeps ids, and leaves out entries with no first name.
- **Folders:** `showDirectoryPicker` decides which explanation the page shows. The first save asks for Documents (`startIn: 'documents'`), creates `social.tennis.app` there (or uses the folder as is if that is what was picked) and remembers the folder handle in IndexedDB; restore opens the file picker inside that folder, or in Documents if none is remembered. A browser cannot write to Documents without the user choosing it, so the first save always asks. Anywhere else the file goes to the share sheet, or is downloaded. Only strings cross the JS interop boundary (trim-safe). The browser tests stub the folder picker and share sheet, since WebKit has neither.
- **Sentences:** "Imported", "Restored" and "Backup of" use "players" for any count, as `spec.md` words them; only "ready to import" has the singular, as `spec.md` says.
- No new packages.
