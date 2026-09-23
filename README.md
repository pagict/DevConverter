# DevConverter

One conversion engine, exposed through PowerToys Run on Windows and Spotlight on macOS.

## Architecture

- `DevConverter.Core` owns all parsing, conversions, output labels, and errors.
- `DevConverter.PowerToys` is the thin Windows UI adapter (the project at repository root).
- `devconvert` is a cross-platform CLI and JSON bridge.
- `DevConverter Spotlight.app` is a native App Intent host that invokes the embedded CLI.

The platform adapters do not contain conversion rules, so the same command produces the same values on both systems. Date formatting uses the local time zone of the machine.

## Commands

```text
dev ts 1719302400
dev tsms 1719302400000
dev date 2026-06-25 17:30:00
dev date now
dev dateutc 2026-06-25T09:30:00Z

dev h2n 0x12345678
dev n2h 0x78563412
dev swap16 0x1234
dev swap32 0x12345678
dev swap64 0x1122334455667788

dev d2b 42
dev b2d 101010
dev d2h 42
dev h2d 0x2A
dev h2b 0x2A
dev b2h 101010

dev ip 192.168.1.1
dev ip2int 192.168.1.1
dev ip2hex 192.168.1.1
dev int2ip 3232235777
dev hex2ip 0xC0A80101

dev b64enc hello
dev b64dec aGVsbG8=
dev urlenc hello world
dev urldec hello%20world
dev hash hello
dev hash sha256 hello
dev hash md5 hello
dev uuid
```

Text commands use UTF-8. `urlenc`/`urlencode` use RFC 3986 percent-encoding (for example, spaces become `%20`); `urldec`/`urldecode` reverse it. `hash` defaults to SHA-256 and also supports MD5, SHA-1, SHA-384, and SHA-512. `guid` is an alias for `uuid`.

## Windows: PowerToys Run

**Requirements:** Windows x64 with [Microsoft PowerToys](https://github.com/microsoft/PowerToys) installed and PowerToys Run enabled. Download the Windows plugin archive, **`DevConverter-PowerToysRun-x64.zip`**, from the [latest DevConverter release](https://github.com/pagict/DevConverter/releases/latest).

### Manual installation (no Git required)

1. Quit PowerToys completely from its system-tray icon.
2. Extract `DevConverter-PowerToysRun-x64.zip`. The archive contains a `DevConverter` folder.
3. Open File Explorer and paste the following into the address bar. Create the `Plugins` directory if it does not already exist:

   ```text
   %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins
   ```

4. Copy the extracted **`DevConverter` folder** into `Plugins`. The resulting layout should be:

   ```text
   Plugins/
   └── DevConverter/
       ├── plugin.json
       ├── Community.PowerToys.Run.Plugin.DevConvert.dll
       └── ...
   ```

   Make sure you do not accidentally nest the folder as `Plugins/DevConverter/DevConverter/`.
5. Start PowerToys again. In **PowerToys Settings → PowerToys Run → Plugins**, make sure **DevConverter** is enabled.
6. Open PowerToys Run (normally **Alt + Space**), enter `dev date now`, and press **Enter** to copy the result.

### Install or update with PowerShell

The repository includes [`scripts/install-windows.ps1`](scripts/install-windows.ps1). Clone or download this repository to obtain the script, then run it from the repository root:

```powershell
git clone https://github.com/pagict/DevConverter.git
cd DevConverter
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-windows.ps1
```

By default, the script downloads the latest public release from GitHub. To install an archive you already downloaded, provide its path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-windows.ps1 -Archive "C:\Downloads\DevConverter-PowerToysRun-x64.zip"
```

The script stops PowerToys, replaces any existing DevConverter plugin folder, copies the new files, and attempts to restart PowerToys. If it does not restart automatically, launch it yourself. No GitHub CLI is required to run the installer; Git is only needed if you choose to clone the repository rather than download its source ZIP.

**Troubleshooting:** If DevConverter does not appear in PowerToys Run, verify that `Plugins\DevConverter\plugin.json` exists, that the plugin is enabled in PowerToys settings, and that PowerToys has been fully restarted. A tagged release may lag the commands documented on `master`; check the release date if a recently added command is missing.

## macOS: Spotlight

The native integration requires macOS Tahoe 26 for Spotlight Quick Keys. Download the archive matching the Mac architecture, then run:

```bash
./scripts/install-macos.sh ./DevConverter-macos-arm64.zip
```

Without an archive argument, the installer downloads the latest public release directly from GitHub; GitHub CLI is not required. After the app has opened once:

1. Open System Settings → Spotlight → Quick Keys.
2. Assign `dev` to the **Dev Convert** action.
3. Open Spotlight and type `dev date now`.

Spotlight runs the App Intent, displays the results, and copies the primary value. Apple does not currently expose a supported API for an installer to assign a Quick Key, so step 2 is intentionally manual.

## Build

```bash
dotnet test tests/DevConverter.Core.Tests/DevConverter.Core.Tests.csproj
dotnet publish src/DevConverter.Cli/DevConverter.Cli.csproj -c Release
```

GitHub Actions tests, builds, and packages Windows x64 plus macOS arm64/x64. After a successful `master` build, it reads the version from `plugin.json` and publishes the matching release automatically; existing release tags are left unchanged.
