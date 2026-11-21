# DogeStrap

DogeStrap is a fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), a third-party replacement for the standard Roblox bootstrapper.
It is **not** affiliated with Roblox Corporation or with the Bloxstrap authors.

> Status: BETA release v1.1.2.

DogeStrap — форк Bloxstrap: альтернативный запускатор Roblox. Проект не связан с Roblox Corporation и авторами Bloxstrap. Статус: BETA-релиз v1.1.2.

## Building

Requirements: Windows 10 or newer, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
git clone --recurse-submodules https://github.com/wrthsiy/DogeStrap.git
cd DogeStrap
dotnet build
```

If you already cloned without `--recurse-submodules`, run `git submodule update --init`.

## Installation

Download the latest published BETA release from [GitHub Releases](https://github.com/wrthsiy/DogeStrap/releases) and run the versioned Windows x64 executable. The framework-dependent build requires the [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows 10 or newer.

## Releases and updates

Pushing a new version tag in `vMAJOR.MINOR.PATCH` format builds a Windows x64 executable using that version and publishes a BETA GitHub release with the matching `DogeStrap-<tag>.exe` asset and license files. Installed production builds check the newest published release, including BETA prereleases, when launching Roblox and can update from that executable asset.

## Credits and license

DogeStrap is based on Bloxstrap by pizzaboxer and contributors, used under the MIT License (see [LICENSE](LICENSE)).
It uses the [WPF UI](https://github.com/lepoco/wpfui) library.
