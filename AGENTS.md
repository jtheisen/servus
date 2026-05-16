# Agent Instructions for Servus

This is a small .NET 9 console application.

## Workflow

- Run pwsh without profile, that's faster
- Don't try to run servus - it has a text-based UI you likely can't work with

## Project basics
- Build command: `dotnet build`
- Run command: `dotnet run`
- Project file: `Servus.csproj`
- Main application logic lives in `Program.cs`
- Data is loaded from `servus.yaml` at startup
- Uses `Spectre.Console` for console rendering and `YamlDotNet` for YAML deserialization

## What to know
- The app displays an interactive tasklet table and refreshes every second
- Navigation keys:
  - Up / Down: move selection
  - E: edit stub
  - D: delete stub
  - Enter: no action assigned
  - Esc: exit
- If `servus.yaml` is missing, the app reports a clean error. Use `servus init` to write a sample file.
- Don't introduce "using Configuration", always qualify the types in there.

## Agent behavior
- Prefer .NET type names over C# ones (String vs string)
- Prefer concise syntax over verbose ones, eg.
  - Primary constructors (if possible)
  - "" over String.Empty
  - Pattern matching over "x == null" (use "x is not null" instead)
  - Don't use private or intenal when it's the default
  - etc
- Dont prefix members with an underscore
- Don't use build-verify, just tell me when dotnet build doesn't go through
  and I need to end a process.
- For publishing a nuget, I prefer the Debug configuration for this project.
