# DogeStrap

DogeStrap is a fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), a third-party replacement for the standard Roblox bootstrapper.
It is **not** affiliated with Roblox Corporation or with the Bloxstrap authors.

> Status: early development, no releases yet.

DogeStrap — форк Bloxstrap: альтернативный запускатор Roblox. Проект не связан с Roblox Corporation и авторами Bloxstrap. Статус: ранняя разработка, релизов пока нет.

## Building

Requirements: Windows, [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).

```
git clone --recurse-submodules https://github.com/wrthsiy/DogeStrap.git
cd DogeStrap
dotnet build
```

If you already cloned without `--recurse-submodules`, run `git submodule update --init`.

## Credits and license

DogeStrap is based on Bloxstrap by pizzaboxer and contributors, used under the MIT License (see [LICENSE](LICENSE)).
It uses the [WPF UI](https://github.com/lepoco/wpfui) library.
