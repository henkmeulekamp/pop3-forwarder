# CLAUDE.md — pop3-forwarder

## Project Overview

`pop3-forwarder` is a minimal .NET 10.0 background service that polls a POP3 inbox and forwards each message via SMTP. It was built to replace Gmail's POP3 access after Google deprecated it in January 2026. The application runs as a single Docker container and is deployed on ARM64/x86_64.

---

## Repository Structure

```
pop3-forwarder/              # Root
├── pop3-forwarder/          # Main .NET project
│   ├── Program.cs           # Host setup, DI registration
│   ├── EmailForwarderService.cs  # All business logic (~212 lines)
│   ├── appsettings.json     # Config template / defaults
│   └── pop3-forwarder.csproj
├── .github/
│   ├── workflows/
│   │   ├── build.yml        # x86_64 CI/CD (push to main → Docker Hub)
│   │   └── build-arm64.yml  # ARM64 build (manual dispatch)
│   └── dependabot.yml       # Weekly dep bumps for NuGet, Actions, Docker
├── Dockerfile               # Multi-stage: sdk:10.0 → dhi.io/dotnet:10-alpine3.22
├── docker-compose.yml       # Local dev / deployment
├── pop3-forwarder.slnx      # Solution file
├── .gitignore
├── .vscode/settings.json    # dotnet.defaultSolution
└── readme.md
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Language | C# 10+ (implicit usings, nullable enabled) |
| Runtime | .NET 10.0 |
| Email protocols | MailKit 4.14.1 (POP3 + SMTP) |
| DI / Hosting | Microsoft.Extensions.Hosting 10.0.2 |
| Configuration | Microsoft.Extensions.Configuration (JSON + env vars) |
| Logging | Microsoft.Extensions.Logging (console, simple formatter) |
| Spam check | Postmark HTTP API (spamcheck.postmarkapp.com) |
| Container base | dhi.io/dotnet:10-alpine3.22 (hardened, low CVE score) |

---

## Architecture

The application follows the standard .NET hosted-service pattern:

```
Program.cs
  └─ IHostBuilder
       └─ EmailForwarderService (BackgroundService)
            ├─ ExecuteAsync()      – 60-second polling loop
            ├─ ForwardEmailsAsync() – POP3 connect → fetch → spam check → forward → delete
            ├─ CheckSpamScoreAsync() – HTTP POST to Postmark spam API
            └─ SendEmailViaSmtpAsync() – SMTP connect → build → send
```

**Key design decisions:**
- POP3 messages are **only deleted after a successful SMTP send** — prevents message loss.
- Spam-check failures are **silently ignored** (score defaults to 0) so email never stalls.
- Malformed `From:` addresses are caught and logged; forwarding continues with a fallback sender.
- All I/O is `async/await`; no blocking calls.

---

## Configuration

Settings are loaded in this priority order (highest first):
1. Environment variables (double-underscore separator: `Pop3Settings__Host`)
2. `appsettings.json` (ships with defaults, mounted in container)

### Required environment variables

```
Pop3Settings__Host
Pop3Settings__Username
Pop3Settings__Password

SmtpSettings__Username
SmtpSettings__Password
SmtpSettings__ForwardTo
```

### Optional variables with defaults

```
Pop3Settings__Port                       = 995
Pop3Settings__UseSsl                     = true
Pop3Settings__CheckCertificateRevocation = false
Pop3Settings__DeleteSpam                 = false

SmtpSettings__Host                       = smtp.gmail.com
SmtpSettings__Port                       = 465
SmtpSettings__UseSsl                     = true
SmtpSettings__CheckCertificateRevocation = false

Logging__LogLevel__Default               = Information
Logging__LogLevel__Microsoft             = Warning
```

---

## Development Workflow

### Build locally

```bash
cd pop3-forwarder
dotnet build
dotnet run
```

### Run with Docker Compose

```bash
# Edit docker-compose.yml with real credentials first
docker compose up --build
```

### Build Docker images manually

```bash
# x86_64
docker build -t pop3-forwarder:dev .

# ARM64 (cross-compile)
docker buildx build --platform linux/arm64 -t pop3-forwarder:arm64 .
```

### Restore / update dependencies

```bash
dotnet restore
# Dependabot opens PRs weekly; merge those to update packages
```

---

## CI/CD

| Workflow | Trigger | Action |
|---|---|---|
| `build.yml` | Push to `main`, PRs | Build only on PRs; build + push x86_64 image on `main` |
| `build-arm64.yml` | Manual `workflow_dispatch` | Build + push ARM64 image |

Both workflows skip when only `readme.md` changes. Images are tagged with `${GITHUB_SHA}` and an architecture tag (`x86_64` / `arm64`). Dependabot PRs are excluded from the push step.

Registries used: Docker Hub (`henkmeulekamp/pop3-forwarding`) and a secondary `dgu` registry.

---

## Code Conventions

- **Namespaces:** File-scoped (`namespace pop3_forwarder;`)
- **Nullable:** Enabled — handle `null` explicitly; use `?` and null-coalescing where appropriate
- **Private fields:** `_camelCase` prefix (e.g. `_logger`, `_configuration`, `_httpClient`)
- **Methods:** PascalCase, async methods end with `Async`
- **Error handling:** Catch specific exceptions, log with `_logger`, do not swallow errors that affect message integrity
- **No tests exist** — if adding tests, use xUnit and mock MailKit interfaces

---

## Key Files to Understand First

1. `pop3-forwarder/EmailForwarderService.cs` — the entire application logic lives here
2. `pop3-forwarder/appsettings.json` — shows every configurable option and its default
3. `Dockerfile` — understand the two-stage build and hardened runtime image
4. `.github/workflows/build.yml` — the main CI/CD pipeline

---

## Spam Filtering

The service calls `https://spamcheck.postmarkapp.com/filter` with the raw email body. If the returned score is **>= 4.0**, the message is treated as spam:
- If `Pop3Settings__DeleteSpam = true` → message is deleted from POP3 without forwarding
- If `Pop3Settings__DeleteSpam = false` (default) → spam messages are skipped but **not** deleted

Any HTTP error from the spam API causes a fallback to score `0.0` (not spam), so the forwarder never blocks legitimate email due to an external service outage.

---

## Security Notes

- Credentials are passed exclusively via environment variables — never commit real values to `appsettings.json`
- The runtime Docker image (`dhi.io/dotnet:10-alpine3.22`) is a hardened image chosen to minimise CVE exposure
- SSL/TLS is enabled by default for both POP3 (port 995) and SMTP (port 465)
- Certificate revocation checking is **disabled by default** for flexibility with self-signed certs; enable with `CheckCertificateRevocation = true` when connecting to public servers

---

## Common Tasks

### Add a new configuration option

1. Add the field to both `appsettings.json` and wherever you read `IConfiguration` in `EmailForwarderService.cs`
2. Document the new env-var in `readme.md` and this file

### Change the polling interval

Edit the `Task.Delay(TimeSpan.FromMinutes(1), stoppingToken)` call in `EmailForwarderService.cs:ExecuteAsync`.

### Change the spam threshold

Edit the constant comparison `spamScore >= 4.0` in `CheckSpamScoreAsync`.

### Update .NET / package versions

Dependabot handles weekly bumps automatically. To bump manually:
```bash
dotnet add package MailKit --version <new-version>
```
Update `Dockerfile` `FROM` tags to match the new SDK/runtime version.
