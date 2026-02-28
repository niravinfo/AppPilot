# AppPilot - Copilot Instructions & Project Specification

## Overview
AppPilot is a .NET 10 desktop application built to manage, control, and health-check various background services and APIs (including Worker Services, gRPC APIs, Web APIs, and Windows Services). 

## Technology Stack
- **Target Framework:** .NET 10
- **UI Framework:** XAML-based (WPF/WinUI)
- **Architecture Pattern:** MVVM (Model-View-ViewModel)

## Core Domains & Directory Structure
- **`/Views`**: XAML UI definitions (e.g., `MainWindow.xaml`). UI logic should strictly remain presentational.
- **`/ViewModels`**: Application presentation logic (e.g., `ServiceItemViewModel.cs`). Must properly implement `INotifyPropertyChanged` (or use a modern MVVM toolkit) and coordinate with backend services.
- **`/Models`**: Data schemas and configuration records (e.g., `AppSettings.cs` matching `appsettings.json` constraints).
- **`/Services`**:
  - **`ServiceControl/`**: Abstractions and implementations for managing lifecycles of processes (`ProcessService.cs`) and system services (`WindowsServiceController.cs`).
  - **`HealthCheck/`**: Monitoring tools for running processes (e.g., `HttpHealthChecker.cs` for HTTP and gRPC pings).
  - **`Configuration/`**: Components responsible for securely loading and managing dynamic application settings (`ConfigurationService.cs`).

## Coding Guidelines & Copilot Rules
1. **.NET 10 Features:** Prefer modern C# features (e.g., primary constructors, collection expressions, raw string literals, pattern matching).
2. **MVVM Strictness:** Never put business, process, or health-check logic in the view's code-behind. Bind Views to ViewModels, and inject Services into ViewModels.
3. **Async / Concurrency:** Use `async` / `await` down to the core, particularly for `Process` creation, `HttpClient` health polling, and UI-blocking operations. Ensure `Task`s are correctly managed so the UI remains completely responsive.
4. **Configuration Mapping:** Settings from `appsettings.json` and `appsettings.Local.json` should map precisely to the strongly-typed classes (like `AppPilot`, `Services[]`).
5. **Resilience & Safety:** Assume services can crash or fail to start. Implement proper try/catch blocks and null checks when interacting with `ExecutablePath` or `ServiceController`. Handle external process output streams asynchronously without deadlocking.