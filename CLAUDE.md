# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build TabularForge.sln

# Build specific project
dotnet build TabularForge.UI/TabularForge.UI.csproj

# Run the application
dotnet run --project TabularForge.UI/TabularForge.UI.csproj

# Run with a BIM file
dotnet run --project TabularForge.UI/TabularForge.UI.csproj -- "path/to/model.bim"

# Clean and rebuild
dotnet clean && dotnet build
```

## Architecture Overview

TabularForge is a WPF desktop application for editing SQL Server Analysis Services (SSAS) Tabular Models (.bim files). It follows **MVVM pattern** with **dependency injection**.

### Solution Structure

```
TabularForge.sln
├── TabularForge.Core/       # Business logic and services
├── TabularForge.DAXParser/  # DAX expression parsing, formatting, semantic analysis
└── TabularForge.UI/         # WPF application (views, viewmodels, themes)
```

### Project Responsibilities

**TabularForge.Core** - Service layer for all business operations:
- `BimFileService`: Loads/saves .bim JSON files, builds TomNode tree structure
- `ConnectionService`: SSAS server connections with Azure AD/MSAL authentication
- `QueryService`, `RefreshService`, `DeploymentService`: Server operations
- `BpaService`: Best Practice Analyzer rules engine
- `UndoRedoManager`: Command pattern for undo/redo (500-level stack)

**TabularForge.DAXParser** - Custom DAX language tooling (no external dependencies):
- `DaxLexer`: Tokenizes DAX expressions
- `DaxParser`: Builds AST from tokens
- `DaxFormatter`: Pretty-prints DAX with configurable formatting
- `SemanticAnalyzer`: Validates DAX semantics, function signatures

**TabularForge.UI** - WPF frontend:
- `MainViewModel` (1300+ lines): Central coordinator managing all sub-viewmodels and commands
- `Views/`: Dockable panels using AvalonDock (TomExplorer, Properties, DAX editors, diagrams)
- `Themes/`: Dark and light theme resource dictionaries

### Key Dependencies

- **AvalonEdit**: DAX syntax highlighting editor (uses `DAX.xshd` embedded resource)
- **AvalonDock**: Dockable panel layout
- **CommunityToolkit.Mvvm**: ObservableObject, RelayCommand attributes
- **Microsoft.AnalysisServices.AdomdClient**: SSAS connectivity
- **Roslyn (4.9.2)**: C# scripting support

### Data Flow

1. User opens `.bim` file → `BimFileService.LoadAsync()` parses JSON
2. `BimFileService` builds `TomNode` tree (hierarchical model representation)
3. `MainViewModel.RootNodes` binds to `TomExplorerPanel` tree view
4. User selects node → `PropertiesPanel` displays editable properties
5. Property changes create `PropertyChangeCommand` for undo/redo
6. Save writes modified `TomNode` tree back to JSON via `BimFileService.SaveAsync()`

### UI Layout (AvalonDock)

- **Left**: TomExplorerPanel (model tree)
- **Center**: Tabbed document pane (Welcome, DAX editors, diagrams)
- **Right**: PropertiesPanel (selected object properties)
- **Bottom**: Messages, Output, Error List panels

### Adding New Features

1. **New Service**: Add to `TabularForge.Core/Services/`, register in `App.xaml.cs` DI container
2. **New Panel**: Create View in `Views/`, ViewModel in `ViewModels/`, add to `MainWindow.xaml` AvalonDock layout
3. **New Command**: Implement `IModelCommand` for undo/redo support

### Important Patterns

- ViewModels use `[ObservableProperty]` and `[RelayCommand]` attributes (source generators)
- All model modifications should go through `UndoRedoManager` for undo support
- DAX syntax highlighting defined in `TabularForge.UI/Highlighting/DAX.xshd`
- Themes are complete resource dictionaries - changes require updating both `DarkTheme.xaml` and `LightTheme.xaml`

## Sample Data

`Samples/AdventureWorks.bim` - Test tabular model for development
