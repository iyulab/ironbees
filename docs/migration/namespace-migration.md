# Migration Guide: Namespace Restructuring

> **Breaking Change**: v0.1.8 → v0.4.1
>
> This guide helps you migrate from the old namespace structure (`Ironbees.AgentMode.Core.*`) to the new consolidated structure.

**Migration Time Estimate**:
- Manual: 5-10 minutes
- Automated (with script): 30 seconds

**Table of Contents**:
- [Quick Reference](#quick-reference)
- [What Changed](#what-changed)
- [Automated Migration](#automated-migration)
- [Manual Migration](#manual-migration)
- [Type Migration Map](#type-migration-map)
- [Package Updates](#package-updates)

---

## Quick Reference

### Search-and-Replace Patterns

| Find | Replace |
|------|---------|
| `using Ironbees.AgentMode.Core.Workflow;` | `using Ironbees.AgentMode.Workflow;`<br/>`using Ironbees.AgentMode.Models;` |
| `using Ironbees.AgentMode.Core.Models;` | `using Ironbees.AgentMode.Models;` |
| `using Ironbees.AgentMode.Core.Agents;` | `using Ironbees.AgentMode.Agents;` |
| `using Ironbees.AgentMode.Core;` | `using Ironbees.AgentMode;` |
| `using Ironbees.AgentMode.Providers;` | `using Microsoft.Extensions.AI;`<br/>`using OpenAI;` |

---

## What Changed

### Package Consolidation

**v0.1.8** (Multiple Packages):
```xml
<PackageReference Include="Ironbees.AgentMode.Core" Version="0.1.8" />
<PackageReference Include="Ironbees.AgentMode.Providers" Version="0.1.8" />
```

**v0.4.1** (Single Package):
```xml
<PackageReference Include="Ironbees.Core" Version="0.4.1" />
<PackageReference Include="Ironbees.AgentMode" Version="0.4.1" />
```

### Namespace Hierarchy

**v0.1.8**:
```
Ironbees.AgentMode.Core
├── Ironbees.AgentMode.Core.Workflow
├── Ironbees.AgentMode.Core.Models
└── Ironbees.AgentMode.Core.Agents

Ironbees.AgentMode.Providers
```

**v0.4.1**:
```
Ironbees.AgentMode
├── Ironbees.AgentMode.Workflow
├── Ironbees.AgentMode.Models
└── Ironbees.AgentMode.Agents

Microsoft.Extensions.AI (external)
```

---

## Automated Migration

### PowerShell Script

**Location**: `scripts/migrate-namespaces.ps1`

**Usage**:
```powershell
# Dry-run (preview changes)
.\scripts\migrate-namespaces.ps1 -ProjectPath . -DryRun

# Apply changes (creates backup first)
.\scripts\migrate-namespaces.ps1 -ProjectPath .

# Apply to specific directory
.\scripts\migrate-namespaces.ps1 -ProjectPath .\src\MLoop.AIAgent
```

**What It Does**:
1. ✅ Backs up all `.cs` files to `.bak`
2. ✅ Replaces old namespaces with new ones
3. ✅ Generates detailed report
4. ✅ Validates changes (optional)

**Example Output**:
```
=== Namespace Migration Tool ===
Project Path: D:\projects\myproject
Mode: Live Migration

Creating backup...
✓ Backed up 42 files

Analyzing files...
✓ Found 15 files with old namespaces

Migrating namespaces...
✓ AgentWorkflowCommand.cs (2 namespaces updated)
✓ WorkflowService.cs (1 namespace updated)
...

Summary:
- Files processed: 42
- Files changed: 15
- Namespaces migrated: 23
- Backup location: .bak files
- Duration: 1.2s

Run 'dotnet build' to verify changes.
```

---

## Manual Migration

### Step 1: Update Using Statements

**Find all occurrences** of old namespaces and replace:

**Pattern 1**: Workflow namespace
```diff
- using Ironbees.AgentMode.Core.Workflow;
+ using Ironbees.AgentMode.Workflow;
+ using Ironbees.AgentMode.Models;
```

**Pattern 2**: Core namespace
```diff
- using Ironbees.AgentMode.Core;
+ using Ironbees.AgentMode;
```

**Pattern 3**: Provider namespace
```diff
- using Ironbees.AgentMode.Providers;
+ using Microsoft.Extensions.AI;
+ using OpenAI;
```

### Step 2: Update Package References

**Remove old packages**:
```xml
<!-- Delete these lines -->
<PackageReference Include="Ironbees.AgentMode.Core" Version="0.1.8" />
<PackageReference Include="Ironbees.AgentMode.Providers" Version="0.1.8" />
```

**Add new packages**:
```xml
<!-- Add these lines -->
<PackageReference Include="Ironbees.Core" Version="0.4.1" />
<PackageReference Include="Ironbees.AgentMode" Version="0.4.1" />
```

### Step 3: Build and Verify

```bash
# Clean build
dotnet clean
dotnet build

# Should succeed with 0 errors
```

---

## Type Migration Map

### Workflow Types

| Type | Old Namespace | New Namespace |
|------|--------------|---------------|
| `WorkflowDefinition` | `Ironbees.AgentMode.Core.Workflow` | `Ironbees.AgentMode.Workflow` |
| `WorkflowRuntimeState` | `Ironbees.AgentMode.Core.Workflow` | `Ironbees.AgentMode.Workflow` |
| `WorkflowExecutionStatus` | `Ironbees.AgentMode.Core.Workflow` | `Ironbees.AgentMode.Workflow` |
| `WorkflowStateType` | `Ironbees.AgentMode.Core.Workflow` | `Ironbees.AgentMode.Workflow` |
| `YamlDrivenOrchestrator` | `Ironbees.AgentMode.Core.Workflow` | `Ironbees.AgentMode.Workflow` |
| `YamlWorkflowLoader` | `Ironbees.AgentMode.Core.Workflow` | `Ironbees.AgentMode.Workflow` |
| `TriggerEvaluatorFactory` | `Ironbees.AgentMode.Core.Workflow.Triggers` | `Ironbees.AgentMode.Workflow.Triggers` |

### Model Types

| Type | Old Namespace | New Namespace |
|------|--------------|---------------|
| `AgentExecutionResult` | `Ironbees.AgentMode.Core.Models` | `Ironbees.AgentMode.Models` |
| `WorkflowExecutionContext` | `Ironbees.AgentMode.Core.Models` | `Ironbees.AgentMode.Models` |
| `ApprovalDecision` | `Ironbees.AgentMode.Core.Models` | `Ironbees.AgentMode.Models` |
| `WorkflowValidationResult` | `Ironbees.AgentMode.Core.Models` | `Ironbees.AgentMode.Models` |

### Agent Types

| Type | Old Namespace | New Namespace |
|------|--------------|---------------|
| `ICodingAgent` | `Ironbees.AgentMode.Core.Agents` | `Ironbees.AgentMode.Agents` |
| `IAgentExecutor` | `Ironbees.AgentMode.Core.Agents` | `Ironbees.AgentMode.Agents` |
| `IAgentExecutorFactory` | `Ironbees.AgentMode.Core.Agents` | `Ironbees.AgentMode.Agents` |

### Removed Types

| Type | Old Namespace | Status |
|------|--------------|--------|
| `LLMProviderFactoryRegistry` | `Ironbees.AgentMode.Providers` | ❌ Removed (use `ChatClientBuilder`) |
| `ConversationalAgent` | `Ironbees.AgentMode.Core.Agents` | ❌ Removed (use Service Layer) |

---

## Package Updates

### Directory.Packages.props (Central Package Management)

**Before**:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Ironbees.AgentMode.Core" Version="0.1.8" />
    <PackageVersion Include="Ironbees.AgentMode.Providers" Version="0.1.8" />
  </ItemGroup>
</Project>
```

**After**:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Ironbees.Core" Version="0.4.1" />
    <PackageVersion Include="Ironbees.AgentMode" Version="0.4.1" />
    <PackageVersion Include="OpenAI" Version="2.1.0" />
    <PackageVersion Include="Microsoft.Extensions.AI.OpenAI" Version="10.1.1" />
  </ItemGroup>
</Project>
```

### .csproj (Project File)

**Before**:
```xml
<ItemGroup>
  <PackageReference Include="Ironbees.AgentMode.Core" Version="0.1.8" />
</ItemGroup>
```

**After**:
```xml
<ItemGroup>
  <PackageReference Include="Ironbees.AgentMode" Version="0.4.1" />
</ItemGroup>
```

---

## Real-World Example: MLoop

**Files Changed**: 3 files
**Time**: 5 minutes (manual), 30 seconds (script)

**Before** (`AgentWorkflowCommand.cs`):
```csharp
using Ironbees.AgentMode.Core.Workflow;
using MLoop.AIAgent.Infrastructure;

public class AgentWorkflowCommand
{
    private static void DisplayWorkflowStates(WorkflowDefinition workflow) { }
    private static void DisplayStateUpdate(WorkflowRuntimeState state) { }
}
```

**After** (`AgentWorkflowCommand.cs`):
```csharp
using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Models;
using MLoop.AIAgent.Infrastructure;

public class AgentWorkflowCommand
{
    private static void DisplayWorkflowStates(WorkflowDefinition workflow) { }
    private static void DisplayStateUpdate(WorkflowRuntimeState state) { }
}
```

**Changes**:
- 1 line removed: `using Ironbees.AgentMode.Core.Workflow;`
- 2 lines added: `using Ironbees.AgentMode.Workflow;` + `using Ironbees.AgentMode.Models;`

---

## Validation

### Build Verification

```bash
dotnet clean
dotnet build

# Expected: 0 errors, 0 warnings (namespace-related)
```

### Test Verification

```bash
dotnet test

# Expected: All tests pass (no namespace-related failures)
```

### Common Errors

**Error**: `CS0234: The type or namespace name 'Core' does not exist`

**Solution**: Change `Ironbees.AgentMode.Core.Workflow` → `Ironbees.AgentMode.Workflow`

**Error**: `CS0246: The type or namespace name 'WorkflowDefinition' could not be found`

**Solution**: Add `using Ironbees.AgentMode.Models;` (moved from Workflow to Models)

---

## Rationale: Why Restructure?

### Problems with v0.1.8 Structure

- ❌ Too many packages (Core, Providers, Models)
- ❌ Circular dependency risks
- ❌ Unclear package boundaries
- ❌ Difficult to version independently
- ❌ `.Core` suffix unnecessary (all ironbees code is "core")

### Benefits of v0.4.1 Structure

- ✅ Single consolidated package (easier versioning)
- ✅ Clear namespace hierarchy (Workflow, Models, Agents)
- ✅ Better alignment with .NET conventions
- ✅ Integration with Microsoft.Extensions.AI (industry standard)
- ✅ Reduced package dependency complexity

### Package Structure Philosophy

```
Ironbees.Core              # Core primitives (agent loading, routing, middleware)
└── Ironbees.AgentMode     # Agent orchestration and workflows
    ├── Workflow           # Workflow execution and state management
    ├── Models             # Data models and DTOs
    └── Agents             # Agent interfaces and execution
```

---

## Additional Resources

### Related Guides
- [ChatClientBuilder Pattern](chatclientbuilder-pattern.md)
- [Service Layer Pattern](service-layer-pattern.md)

### Tools
- [Migration Script](../../scripts/migrate-namespaces.ps1)
- [ReSharper Profile](../../scripts/namespace-migration.DotSettings) (optional)

### Support
- [GitHub Issues](https://github.com/iyulab/ironbees/issues)
- [Discussions](https://github.com/iyulab/ironbees/discussions)

---

**Last Updated**: 2026-01-06
**Validated By**: MLoop Team
