# CHANGELOG

## [Unreleased]

### ⬆️ Upgraded
- **.NET 9 → .NET 10** - All projects now target net10.0
- **Aspire 9.0.0 → 13.1.2** - Latest Aspire with enhanced features
  - `Aspire.Hosting` 13.1.2
  - `Aspire.Hosting.Azure.ServiceBus` 13.1.2
  - `Aspire.Azure.Messaging.ServiceBus` 13.1.2 (new)

### ✨ Added
- **Aspire Azure Messaging ServiceBus Integration** - Automatic client registration via `AddAzureServiceBusClient()`
- **Enhanced AppHost** - Better Service Bus Emulator orchestration with `RunAsEmulator()` support
- **New Documentation** - UPGRADE.md guide for the migration

### 🔧 Changed
- **AppHost Program.cs** - Uses new Aspire APIs and Azure namespace
- **PicoBusX.Web Program.cs** - Now uses integrated `AddAzureServiceBusClient()` for better DI management
- **Documentation** - Updated all references to .NET 10 and Aspire 13.1.2

### 📝 Docs
- Updated README.md with new features and requirements
- Enhanced AppHost README with Aspire integrations section
- Created UPGRADE.md with migration guide

## [Previous Release]
See git history for earlier versions.

