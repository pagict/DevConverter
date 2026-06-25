# DevConvert PowerToys Run Plugin

A small PowerToys Run plugin for developer conversions.

Action keyword: `dev`

## Commands

```text
dev ts 1719302400
dev tsms 1719302400000
dev date 2026-06-25 17:30:00
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
```

Press Enter on a result to copy it.

## Build without installing C# tooling locally

The easiest way is GitHub Actions:

1. Create a new GitHub repository.
2. Upload these files.
3. Open the repository's **Actions** tab.
4. Run **Build plugin** manually.
5. Download the generated artifact `DevConvert-PowerToysRun-x64.zip`.

## Install

1. Close PowerToys completely.
2. Extract the zip.
3. Copy the extracted `DevConvert` folder to:

```text
%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins
```

4. Start PowerToys again.
5. Open PowerToys Run and type `dev`.

## Local build, optional

If you later install .NET SDK:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained false -p:Platform=x64 -o publish\DevConvert
```
