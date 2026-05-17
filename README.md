# Servus

Servus is a small terminal UI for starting and watching local services during development.

> Maturity warning: As of now, platforms other than Windows don't request a
> graceful exit (ctrl-c) for started services and resort to killing them.
> Windows does this only as a fallback. Otherwise, my biggest maturity concern
> is that services may stay alive under certain edge conditions. This will
> hopefull be ironed out in the future.

## Install

Servus is distributed as a .NET tool:

```powershell
dotnet tool install --global IronStone.Servus
```

Update an existing installation:

```powershell
dotnet tool update --global IronStone.Servus
```

Uninstall:

```powershell
dotnet tool uninstall --global IronStone.Servus
```

## First Run

Create a sample configuration in the current directory:

```powershell
servus init
```

Edit the generated `servus.yaml`, then start the UI:

```powershell
servus
```

Use another configuration file:

```powershell
servus --config path\to\servus.yaml
```

## Configuration

`servus.yaml` defines reusable profiles and tasks. The generated sample shows the available task options, including commands, working directories, profiles, shortcuts, ports, and connection checks.

## Requirements

- .NET 9 or newer runtime
