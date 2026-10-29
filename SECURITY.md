# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Pulse, please report it privately:

1. **GitHub Private Vulnerability Reporting**: Use the "Report a vulnerability" button in the [Security Advisory tab](https://github.com/charles-forsyth/pulse-windows/security/advisories/new).
2. Alternatively, email **charles.forsyth@ucr.edu** with the subject `[SECURITY] Pulse Windows`.

Please provide detailed information to help reproduce and resolve the issue:
- Type of issue (e.g. privilege escalation, memory safety, unsafe Win32 interop)
- Location of affected source code
- Step-by-step reproduction instructions or proof of concept

### Scope Note
Pulse is a local-only desktop monitoring application that operates without telemetry collection, external network calls, authentication tokens, or credential storage. It queries standard Win32, PerformanceCounter, and WMI interfaces on the local machine.
