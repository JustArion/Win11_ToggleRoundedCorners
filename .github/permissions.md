### Technical
The technical write-up can be found [here](./technical.md)

---

## Key
- __Can__ = Possible, but needs to be initiated by the user

## Permissions
- `Admin`
    - Requires to be run as Administrator
        - Reading Process Memory
        - Writing Process Memory
- `IO`
    - `Write Access`
        - Writes a single Log File to the `ToggleRoundedCorners.exe` directory
            - `ToggleRoundedCorners.log`
        - Copies (`symsrv.dll`) to the `ToggleRoundedCorners.exe` directory
            - `symsrv.dll` is found in either `amd64`, `arm64` or `x86` folders in the `ToggleRoundedCorners.exe` directory
        - __Can__ write (`dbghelp.dll`) to the `ToggleRoundedCorners.exe` directory
            - The file is copied from `/Windows/System32/dbghelp.dll`
        - __Can__ create a folder called `sym` in the `ToggleRoundedCorners.exe` directory
        - __Can__ write debug symbols to the `sym` folder in the `ToggleRoundedCorners.exe` directory
    - `Read Access`
        - __Can__ read the file `dbghelp.dll` from `/Windows/System32/`
    - `Delete Access`
        - Recursively deletes folders
            - `amd64` from the `ToggleRoundedCorners.exe` directory
            - `arm64` from the `ToggleRoundedCorners.exe` directory
            - `x86` from the `ToggleRoundedCorners.exe` directory
- `Persistence`
    - `Task Scheduler`
        - `Startup`
            - __Can__ create 1 startup task (Task Scheduler)
                - Task runs on Logon (Any user)
                - On `Events`
                    - Log: `System`, Source: `Microsoft-Windows-Kernel-Power`, Id: `107`
                        - "The system has resumed from sleep."
                        - When started from sleep
                    - Log: `Application`, Source: `Desktop Window Manager`, Id: `9027`
                        - "The Desktop Window Manager has registered the session port."
                        - If `dwm.exe` restarts
        - Creates 1 manual start task (Task Scheduler)
            - Can execute `ToggleRoundedCorners.exe` as admin
            - Expires if unused in 15 days
- `Network`
    - `Upload Access`
        - Sends telemetry (`http://localhost:9999`)
            - This does not send telemetry to somewhere online (default)
            - Configurable by the user / command line, Handled by Nuget package [Serilog.Sinks.Seq](https://www.nuget.org/packages/Serilog.Sinks.Seq) & external application if present on PC ([Seq](https://datalust.co/seq))
    - `Download Access`
        - __Can__ download files from `https://msdl.microsoft.com/download/symbols`
            - Debug symbols are downloaded to the `sym` folder in the `ToggleRoundedCorners.exe` directory
- `Process`
    - `Read Access`
        - __Can__ read from the process `dwm.exe`
            - __Can__ read from the process module `udwm.dll`
    - `Write Access`
        - __Can__ write to process `dwm.exe`
            - __Can__ write to process module `udwm.dll`
    - `Terminate Access`
        - __Can__ terminate `dwm.exe`