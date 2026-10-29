# Contributing to Pulse

Thank you for your interest in improving Pulse!

## Development Workflow

1. **Fork and Branch**: Create a feature branch off `main` (e.g., `feature/custom-gauges` or `fix/thread-count-leak`).
2. **Build and Test**:
   - Install .NET 8.0 SDK and Visual Studio 2022 / Windows App SDK workload.
   - Run `dotnet build Pulse.slnx` or publish using `tools/publish.ps1`.
3. **Commit Cleanly**: Write clear, descriptive commit messages.
4. **Pull Requests**: Open a pull request against `main`. All pull requests are reviewed and squash-merged.

## Code Standards

- Adhere to modern C# 12 and .NET 8 idioms (nullable reference types, pattern matching, records).
- Maintain responsiveness: heavy Win32 or WMI queries must execute on background threads without blocking UI dispatch.
- Keep executables fully self-contained: avoid dependencies on non-standard system runtimes.
- Avoid any telemetry tracking or external analytics phone-homes.
