Start task $ARGUMENTS of Social Tennis on its own branch, then implement it.

Follow these steps in order. Stop and ask the user if any check fails.

1. Check the working tree with `git status --short`. If there are uncommitted changes, stop and tell the user what they are.
2. Check that `feature/task$ARGUMENTS` does not already exist (`git branch --list feature/task$ARGUMENTS`). If it exists, stop and ask whether to continue on it.
3. Run `git checkout develop`, then `git checkout -b feature/task$ARGUMENTS`. Never commit task work to `develop` or `master`.
4. Read `app.claude.tennis/docs/spec.md` and `app.claude.tennis/docs/task$ARGUMENTS.md`, then implement the task as written. Follow `app.claude.tennis/docs/codingStandard.md` and `app.claude.tennis/docs/projectStandards.md`. Write tests for every requirement.
5. Meet the Definition of Done in `projectStandards.md` section 7: zero warnings and errors in Debug and Release, the full test suite passing (`dotnet test`), and `CLAUDE.md` updated, including the task's progress tick and any decisions.
6. Commit on `feature/task$ARGUMENTS` with a short imperative subject that names the task, for example "Implement task$ARGUMENTS", with the Co-Authored-By line.
7. Do not push, and do not merge. Tell the user the task is ready and that `/finish-task $ARGUMENTS` merges it into `develop`.
