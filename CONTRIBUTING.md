# Contributing

Thanks for helping. This is a small project, so the rules are short.

## Set up

Follow "Run it for development" in the [README](README.md). You need the .NET 10 SDK and Node.js 22.

## Before you open a pull request

```bash
dotnet build
dotnet test
cd web && npm run build && npm test -- --watch=false
```

CI runs the same commands. A pull request should:

- change one thing, with a description of why
- include tests for new behaviour (domain rules and calculators get unit tests; endpoints get an integration test)
- leave the Angular production build free of warnings

## Changing the data model

1. Change the entity and its configuration in `ApplyTrail.Infrastructure/Persistence/AppDbContext.cs`.
2. Add a migration and commit the generated files:

   ```bash
   dotnet ef migrations add <WhatChanged> -p src/ApplyTrail.Infrastructure -s src/ApplyTrail.Api -o Migrations
   ```

CI fails if the model and the migrations disagree.

## Conventions

- **Domain first.** If a rule can be expressed on an entity, put it there and test it without a database.
- **Every query is scoped to the signed-in user.** New endpoints must filter by `currentUser.Id`, and a missing item and someone else's item must look the same (404).
- **No raw exceptions to clients.** Throw an `AppException` subclass for expected failures; the exception handler turns it into a problem response.
- **Frontend.** Standalone components, signals for state, native `<dialog>`, no component-library dependency. Keep the interface keyboard-usable and check it in light and dark mode.
- **Commits.** Short imperative subject ("Cancel follow-ups when a reply arrives"), and a body that says why when it is not obvious.

## Reporting a security problem

Please do not open a public issue. Contact the maintainer privately through the email on their GitHub profile.
