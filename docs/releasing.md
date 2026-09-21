# Releasing

The library targets .NET 10 and has no runtime NuGet dependencies. The SDK policy rolls forward within .NET 10 rather than requiring one exact installed SDK patch.

## Local checks

```shell
dotnet build EsoData.sln -c Release
dotnet test EsoData.sln -c Release --no-build
dotnet pack src/EsoData/EsoData.csproj -c Release --no-build -o artifacts/packages
```

Packages contain the README, MIT license, XML documentation and Source Link metadata, with a separate symbol package. CI runs the build, tests and pack on Windows and Linux. No game installation or private save files are required.

## One-time NuGet setup

Use [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) for GitHub Actions:

- NuGet user: `JuliusJacobsohn`
- GitHub owner: `JuliusJacobsohn`
- Repository: `EsoData.NET`
- Workflow: `release.yml`
- Environment: none
- Package scope: `EsoData.NET`, including permission for the initial package

Create this repository's policy in the NuGet account before publishing. AnkiIO's existing policy does not automatically authorize a different repository. No permanent NuGet API key is stored in this repository.

## Publish

1. Update `Version.props` and commit/push the change with a semantic message, for example `chore: release 0.2.0`.
2. Wait for CI to pass.
3. Create and push the matching tag: `git tag v0.2.0` then `git push origin v0.2.0`.
4. The Release workflow builds/tests/packs, authenticates with NuGet using OIDC, publishes the package, then creates a GitHub release with package and symbol artifacts.

The tag must match the package version. Never move an existing published release tag; make a new version. Alternatively run the Release workflow manually: its default is a package-only dry run, and the `publish` input enables publication. Select the intended release commit on `main` when using this option.

If NuGet authentication fails, the package artifacts still appear on the Actions run. Complete/fix the trusted publishing policy and rerun the release. A built package or GitHub release alone does not mean NuGet publication succeeded.
