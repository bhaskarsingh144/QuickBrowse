# Contributing to QuickBrowse

Thank you for your interest in contributing to QuickBrowse! This document provides guidelines for contributing to the project.

## Getting Started

### Prerequisites

- Windows 10 or Windows 11
- .NET Framework 4.5 or later (comes pre-installed with Windows)
- Basic knowledge of C# and Windows Forms
- C# 5 only in this tree (no interpolated strings, no `out var`)

### Git setup (required once per clone)

Install the local `commit-msg` hook so Cursor cannot inject `Co-authored-by: Cursor` trailers:

```powershell
.\scripts\install-git-hooks.ps1
```

Also turn **off** Cursor Settings → **Git & PRs** → **Attribution** (Commit Attribution and PR Attribution).

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/bhaskarsingh144/QuickBrowse.git
   cd QuickBrowse
   ```

2. Run the build script:
   ```powershell
   .\scripts\build.ps1
   ```

3. The compiled executable will be in `bin\QuickBrowse.exe`

4. To run standalone (no installation):
   ```batch
   .\scripts\setup-standalone.bat
   ```

5. To install system-wide:
   ```batch
   .\scripts\setup.bat
   ```
   (Run as Administrator)

## Project Structure

```
QuickBrowse/
├── src/                       # C# source
│   ├── Program.cs             # Entry point and startup
│   ├── ShellHost.cs           # Park/reveal tick, idle, tray
│   ├── ShellChrome.cs         # Shared Decide / ZOrder / Left edge
│   ├── RailForm.cs            # Places rail
│   ├── BrowseForm.cs          # Connected browse pane
│   ├── PinsFlyoutForm.cs      # Pins / Recent flyout
│   └── ...
├── tests/                     # Unit tests (see scripts/test.ps1)
├── scripts/
│   ├── build.ps1              # Compile from source
│   ├── test.ps1               # Run unit tests
│   ├── setup.bat              # System-wide install (admin)
│   └── setup-standalone.bat   # Portable build + run.bat
├── docs/                      # Manual test matrices
├── assets/                    # README screenshots
├── bin/                       # Generated output
├── LICENSE
├── CONTRIBUTING.md
└── README.md
```

## Development Guidelines

### Code Style

- Use 4 spaces for indentation
- Follow C# naming conventions (PascalCase for public members, camelCase for private)
- Keep methods focused and concise
- Prefer patterns already used in JumpDock / QuickView for shell chrome

### Testing

Automated (no NuGet):

```powershell
.\scripts\test.ps1
```

Manual matrices live in `docs/TEST-*.md`. Before submitting a pull request:

1. Build successfully (`.\scripts\build.ps1`)
2. Pass `.\scripts\test.ps1`
3. Run the app and verify left-edge park/reveal
4. Spot-check coexistence with JumpDock and QuickView if those are installed

### Submitting Changes

`main` is the default branch. Prefer pull requests for collaborative changes.

1. Create a feature branch from the latest `main`
2. Commit with clear messages (`feat:`, `fix:`, `docs:`, `chore:`)
3. Open a pull request with summary + test plan
4. Keep PRs focused when you can

### Commit Message Format

- `feat: denser Pins flyout rows`
- `fix: left hover park after Win+D`
- `docs: update TEST-SHELL matrix`
- `chore: align DisplayScale with QuickView`

## Known Limitations

- Empty-desktop Rest (always-visible chrome with no apps open) may still evolve with siblings
- Shell context menus depend on Windows Explorer verb availability per item type
- Recent list quality depends on the Windows Recent folder

## Reporting Issues

When reporting bugs, please include:

- Windows version
- Screen resolution and DPI / display scale
- Steps to reproduce
- Expected vs actual behavior
- Crash / error logs from `%LOCALAPPDATA%\QuickBrowse\logs\` (`last-fatal.log`, recent `crash_*.log`, and `app.log`)

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE file).
