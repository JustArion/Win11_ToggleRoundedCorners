<img src="\Images\Show-Sharp.png" width="450" height="350"/>

> [!NOTE]
> If you run the normal version and you don't have the required prerequisites installed it will prompt you if you want to install them, opening a link to Microsoft to download.
> __There are 2 prompts of this, 1 per runtime needed__

## Table of Contents
- [Requirements](#requirements)
- [Custom Launch Args](#custom-launch-args)
- [Previews](#previews)
- [Building from Source](#building-from-source)
- [Supported Architectures](#supported-architectures)
- [Permissions](#permissions)
- [Thank Yous](#thank-yous)
---

## Requirements
- Windows 11

### Running Normal:
- [.NET 9.0 Desktop Runtime](https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch=x64&rid=win-x64&os=win10&apphost_version=9.0.1&gui=true) (±59mb)
- [ASP.NET Core 9.0 Runtime](https://aka.ms/dotnet-core-applaunch?framework=Microsoft.AspNetCore.App&framework_version=9.0.0&arch=x64&rid=win-x64&os=win10&gui=true) (±10mb)
### Running Portable:
- None (Though has considerably bigger file size)

---
### Custom Launch Args

| Argument           |      Default Value       | Description                                                                                |
|:-------------------|:------------------------:|:-------------------------------------------------------------------------------------------|
| --sharp-corners    |          `N/A`           | Set sharp corners when starting up                                                         |
| --seq-url=         |  http://localhost:9999   | Seq Logging Platform                                                                       |
| --bind-to=         |          `N/A`           | Binds this process to another process' ID. When the other process exits, this one does too |
| --extended-logging |          `N/A`           | File Log Level: Verbose (From Warning)                                                     |
| --headless         |          `N/A`           | Runs the application without a GUI, useful when combined with `--sharp-corners`            |
| --no-file-logging  |          `N/A`           | Disables logging to the file (Located in the current directory)                            |

**Launch Args Example**

`& '.\ToggleRoundedCorners.exe' --extended-logging --headless --sharp-corners --seq-url=http://localhost:9999`

## Previews

Previews can be found [here](./previews.md)

---
## Building from Source
#### Requirements:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [Git](https://git-scm.com/downloads)

#### Manual:
```ps1
git clone 'https://github.com/JustArion/Win11_ToggleRoundedCorners'; cd 'Win11_ToggleRoundedCorners'
dotnet publish .\src\ToggleRoundedCorners\ --configuration Release --output Build
cd build; ls
```

#### Makefile:
```ps1
git clone 'https://github.com/JustArion/Win11_ToggleRoundedCorners'; cd 'Win11_ToggleRoundedCorners'
make
```

---
## Supported Architectures
- x64

Create an issue to request additional architectures.

---
## Permissions

A comprehensive list of permissions the application needs / could need can be found [here](permissions.md)

---
### Thank Yous
- Thanks to [Rckov](https://github.com/Rckov) for his window theme from [Xslt-Editor](https://github.com/Rckov/Xslt-Editor)
