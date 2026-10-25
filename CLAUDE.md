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

- Repo root: `SocialTennis.slnx` (every project), `Directory.Build.props` (shared build settings and StyleCop), `.editorconfig`, `stylecop.json`, `scripts/`, `.github/workflows/deploy.yml`, `README.md`, `.gitignore` and this file.
- `app.claude.tennis/`: the .NET 10 Blazor WebAssembly PWA (root namespace `SocialTennis`).
  - `Navigation/`: plain logic for the toolbar items and header titles (`AppNavigation`).
  - `Layout/`: `MainLayout` (header, content, toolbar), `BottomToolbar`, `ToolbarIcon`, `ConfirmDialog` (centred confirmation), and the schedule components `CourtsStep`, `PlayersStep`, `ScheduleReview`, `ScheduleCards` (round cards, shared by Review and Play), `PlayerLabel`.
  - `Players/`: plain logic: `Player`, `PlayerValidator`, `PlayerList` (search and sort), `PlayerService` (storage), `PlayerJsonContext`.
  - `Scheduling/`: plain logic: `Schedule` (and `Round`, `Game`, `SchedulePlayer`, `ScheduleStep`), `ScheduleEngine` (the schedule rules), `ScheduleFlow` (create steps, play, reorder), `CourtList` (parse courts), `PlayerNames` (short names), `ScheduleService` (storage), `ScheduleStorageJsonContext`.
  - `Storage/`: `IKeyValueStorage` and `LocalStorage` (browser localStorage via JS interop).
  - `Pages/`: thin routed pages (Play `/`, Players `/players`, PlayerEdit `/players/add` and `/players/edit/{id}`, SchedulePage `/schedule`, About, NotFound).
  - `wwwroot/`: `index.html`, `manifest.webmanifest`, icons, `js/pwa.js`, `js/reorder.js` (drag to reorder), `service-worker.js` (dev, no-op) and `service-worker.published.js` (offline cache, used by publish).
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
- Publish for GitHub Pages: `scripts/publish.sh <output-dir> /<repo-name>/` writes the site to `<output-dir>/site` (rewrites `<base href>`, adds `404.html` for deep links and `.nojekyll`).

## Architecture (current state)

- Entry point `Program.cs` mounts `App` on `#app`. `App.razor` routes with `MainLayout`; `NotFound` is the not-found page.
- `MainLayout` shows an `h1` header with the current page label (from `AppNavigation.TitleFor`, updated on `LocationChanged`), the page in a scrolling `main`, and `BottomToolbar` (four `NavLink`s with SVG icons) at the bottom. Safe-area insets are respected. Light and dark colours are CSS variables in `wwwroot/css/app.css` (follows the device; a `data-theme` override is ready for the About page task).
- Offline: `service-worker.published.js` precaches every file in the generated `service-worker-assets.js` manifest (no integrity checks, see Decisions), answers navigations with the cached `index.html`, deletes older caches on activate, and takes over at once (`skipWaiting` + `clients.claim`). `js/pwa.js` registers it and reloads once when a new version takes control.
- Players (task 2): `Players.razor` lists, searches and sorts; `PlayerEdit.razor` adds, edits and deletes (with `ConfirmDialog`). Players are one JSON list in localStorage (`socialTennis.players`) and the sort choice is stored as `socialTennis.playerSort`; `PlayerService` is the only code that touches them. Serialisation is source-generated for trimming.
- Schedule (task 3): one schedule in localStorage (`socialTennis.schedule`), saved after every step and resumed at the same `ScheduleStep` (Courts, Players, Review, InPlay). `SchedulePage` switches between `CourtsStep`, `PlayersStep` and `ScheduleReview`. `ScheduleEngine.Generate(players, courtCount, seed)` is a seeded randomised search; the Play page shows an in-play schedule read only with court numbers. Drag to reorder uses pointer events in `js/reorder.js`, which only reports the move back to `ScheduleCards`.
- About is a placeholder until a later task fills it in.

## Maintaining this file

Update this file as part of finishing each task, not afterwards:

- Keep "Repository layout", "Commands" and "Architecture" matching the code (projects, folders, how to build, test, run browser tests, publish).
- Record each decision where the spec or task left a choice open, and each new package or abstraction with its licence and reason (required by `codingStandard.md`).
- Don't copy rules from the standards files here. Link to them so there is one source of truth.

## Task progress

- [x] task1
- [x] task2
- [x] task3
- [ ] task4
- [ ] task5
- [ ] task6

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
- **Edit button on Review** is shown disabled: the task says it is defined later. The Play page has an Edit link to Review (as the task asks), and Review hides the "‹ Players" link once the schedule is in play.
- **Courts input:** blank entries from stray commas are ignored (`4,5,` is courts 4 and 5); a negative number gets "Court numbers must be 1 or more."
- **Players step:** "Add" needs a suggestion to be picked first (as the spec says). Each add/remove is saved. Players are copied into the schedule, and names and ranks are refreshed from the player list when the schedule is generated.
- **Generating** waits 50 ms before the (blocking) search so "Matching players…" can paint; the seed comes from `Random.Shared` in the page and is stored in the schedule.
- **Layout fix:** `#app.loading` stays a centring grid after load, which let a tall page grow the shell past the screen; it now has a one-row grid.
- **Not done (later tasks):** Edit mode, pins, swaps, Re-schedule, round start/finish, Close schedule, and sending the user to Players when nothing is in play.
- No new packages.
