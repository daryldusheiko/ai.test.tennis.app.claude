# ai.test.tennis.app.claude

Social Tennis: an offline-first iPhone PWA for scheduling social tennis doubles, built with .NET 10 and Blazor WebAssembly. The product is described in `app.claude.tennis/docs/spec.md`.

## Branches

- `master`: the release branch. Every push deploys to GitHub Pages.
- `develop`: the integration branch. Finished tasks are merged here.
- `feature/taskN`: one branch per task, cut from `develop`.
- `feature/<name>`: other changes, such as this workflow, cut from `develop` the same way.

Never commit task work directly to `develop` or `master`.

## Starting a feature (task)

Run this in Claude Code from the repo root:

```
/start-task N
```

It checks that the working tree is clean and that `feature/taskN` does not exist yet. It then switches to `develop`, creates `feature/taskN`, implements the task and commits. It doesn't push or merge.

To do it by hand:

```
git checkout develop
git checkout -b feature/taskN
```

## Finishing a feature (task)

Run this in Claude Code from the repo root:

```
/finish-task N
```

It asks you to confirm, runs the Release build and the tests, and merges `feature/taskN` into `develop` with `--no-ff`. It doesn't push.

To do it by hand, after the tests pass:

```
git checkout develop
git merge --no-ff feature/taskN
```

Then delete the branch if you no longer need it: `git branch -d feature/taskN`.

## Pushing and releasing

- Pushing `develop` is a separate step: `git push origin develop`.
- To release, open a pull request from `develop` into `master`. Merging it deploys the app.

## Build and test

Run from the repo root:

- `dotnet run --project app.claude.tennis`: runs the app at http://localhost:3000.
- `dotnet build`: builds everything. Use `dotnet build -c Release` for the release gate.
- `dotnet test app.claude.tennis.Tests`: unit tests.
- `dotnet test`: all tests, including the browser tests in WebKit (the first run downloads WebKit).
- `scripts/publish.sh <output-dir> /<repo-name>/`: publishes the static site.

The project rules are in `CLAUDE.md` and `app.claude.tennis/docs/`.
