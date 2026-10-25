# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

- Repo root holds a minimal `README.md` and a .NET-style `.gitignore`.
- The app lives in `app.claude.tennis/`, a .NET 10 Blazor WebAssembly project (`Microsoft.AspNetCore.Components.WebAssembly` 10.0.13, `Nullable` and `ImplicitUsings` enabled).
- No solution (`.sln`) file was found at the root, and no test project was found in the files inspected.

## Commands

Run from the repo root:

- Run (dev): `dotnet run --project app.claude.tennis` — serves on `http://localhost:5216` and `https://localhost:7028` (per `Properties/launchSettings.json`), with `ASPNETCORE_ENVIRONMENT=Development`.
- Build: `dotnet build app.claude.tennis`

These commands are taken from the project files and have not been run in this session.

## Architecture

- Entry point: `app.claude.tennis/Program.cs` builds a `WebAssemblyHostBuilder`, mounts `App` on `#app`, and registers a scoped `HttpClient` whose base address is the host's base address.
- Routing: `App.razor` wraps a `Router` that uses `MainLayout` as the default layout and `Pages/NotFound.razor` as the 404 page. Pages declare routes with `@page`.
- Layout: `Layout/MainLayout.razor` renders `NavMenu` in a sidebar and `@Body` in the main content area. `NavMenu.razor` holds the nav links.
- Shared Razor imports are in `_Imports.razor`.
- Static assets: `wwwroot/index.html` loads Bootstrap from `lib/bootstrap` and `css/app.css`.

The project is still the default Blazor template. The Home, Counter, and Weather pages and the nav links to them are placeholders. Replace them as the tennis features are built.
