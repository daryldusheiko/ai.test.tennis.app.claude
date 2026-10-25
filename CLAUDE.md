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
  - `Layout/`: `MainLayout` (header, content, toolbar), `BottomToolbar`, `ToolbarIcon`.
  - `Pages/`: thin routed pages (Play `/`, Players, Schedule, About, NotFound).
  - `wwwroot/`: `index.html`, `manifest.webmanifest`, icons, `js/pwa.js`, `service-worker.js` (dev, no-op) and `service-worker.published.js` (offline cache, used by publish).
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
- Pages are placeholders until the later tasks fill them in.

## Maintaining this file

Update this file as part of finishing each task, not afterwards:

- Keep "Repository layout", "Commands" and "Architecture" matching the code (projects, folders, how to build, test, run browser tests, publish).
- Record each decision where the spec or task left a choice open, and each new package or abstraction with its licence and reason (required by `codingStandard.md`).
- Don't copy rules from the standards files here. Link to them so there is one source of truth.

## Task progress

- [x] task1
- [ ] task2
- [ ] task3
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
