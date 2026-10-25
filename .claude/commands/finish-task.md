Finish task $ARGUMENTS of Social Tennis by merging `feature/task$ARGUMENTS` into `develop`.

Follow these steps in order. Stop and ask the user if any check fails.

1. Check the working tree with `git status --short`. If there are uncommitted changes, stop and tell the user what they are.
2. Confirm with the user before merging. Merging changes `develop`, so get an explicit yes.
3. Check that the tests pass on `feature/task$ARGUMENTS`: run `dotnet build -c Release` and `dotnet test`. If anything fails, stop and report the output.
4. Run `git checkout develop`, then `git merge --no-ff feature/task$ARGUMENTS`.
5. Confirm with `git log --oneline -3 develop` that the merge is there.
6. Do not push unless the user asks. Remind the user that pushing `develop` is a separate step.
