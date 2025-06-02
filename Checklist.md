**Note**: this file was created by external AI Bot (Google Jules). it might not be fully accurate.


# Currently Implemented

*   **Modern WPF UI:** Clean user interface based on WPF, with support for light and dark themes.
*   **Project Creation (Fabric & Forge):** Ability to create new mod projects for both Fabric and Forge mod loaders. This process is template-based.
*   **Mod Metadata Definition:** During project creation, users can define essential metadata such as:
    *   Mod Name
    *   Mod ID (with auto-suggestion based on name)
    *   Package Name (with auto-suggestion based on Mod ID)
    *   Minecraft Version
    *   Mod Loader (Fabric/Forge)
    *   License
    *   Description
    *   Authors
    *   Mod Version (defaults to 1.0.0)
    *   Project Icon
*   **Dashboard:**
    *   Lists existing Modrix projects found in the local application data.
    *   Allows filtering projects by name/Mod ID, game version, and mod loader.
    *   Provides functionality to delete projects (including files from disk).
    *   Allows opening the project's folder in the file explorer.
    *   Button to refresh the project list.
    *   Button to open the main projects folder.
*   **Gradle Integration (Initial Setup):**
    *   For new projects, automatically copies a Gradle wrapper.
    *   Runs a Gradle task (e.g., `genIntellijRuns` for Forge) to set up the project for IDEs.
*   **Project Configuration Storage:**
    *   Saves a `modrix.config` file within each project's directory, storing its core metadata, which is used to repopulate the dashboard.
*   **Input Validation:** Basic validation and formatting for fields like Mod ID and Package Name during project creation.
*   **Theme Management:** Saves and loads user's theme preference (light/dark).
*   **Connectivity Check:** Checks for internet connectivity on startup and can display an offline notification.

# Half-Implemented

*   **Fabric Project Generation:**
    *   Successfully creates a Fabric project structure by copying and modifying template files.
    *   Updates `fabric.mod.json`, `gradle.properties`, Java package names, and mixin configurations based on user input.
    *   Includes a `JdkHelper` to verify Java environment (details of its thoroughness are not fully known).
    *   **Missing:** Unlike Forge project generation, it does not automatically run Gradle tasks (e.g., `genSources`, `genIntellijRuns`) to prepare the project for immediate IDE use. Users might need to manually run these.
*   **Project Workspace (`Views/Pages/WorkspacePage.xaml`):**
    *   This is intended to be the main area for managing a loaded project's content and settings.
    *   The current C# code-behind is empty (`InitializeComponent()` only).
    *   Actual functionalities are undefined or exist only in XAML, indicating it's likely a placeholder or significantly incomplete.
*   **Language Control (`Views/Pages/LanguageControlPage.xaml`):**
    *   A dedicated page exists in the project workspace menu.
    *   The current C# code-behind is empty (`InitializeComponent()` only).
    *   Its purpose and functionality are unknown, suggesting it's a placeholder or not yet implemented.
*   **Resource Manager (`Views/Pages/ResourcesPage.xaml` and `ViewModels/Pages/ResourcesPageViewModel.cs`):**
    *   **Implemented Basics:**
        *   Lists existing textures (.png, .jpg), models (.json), and sounds (.ogg) from project folders.
        *   Allows importing new files for these types (copies them to the correct asset subdirectories).
        *   Allows changing/removing the project's `icon.png`.
        *   Includes a basic text editor for the project's `README.md` file (load/save).
        *   Provides "Open Folder" links for asset types.
        *   Basic context menu for textures (Open, Delete).
        *   Can play `.ogg` sound files.
    *   **Missing/Areas for Improvement:**
        *   No preview for models.
        *   No specialized editors for any resource type other than plain text for README.
        *   No management of metadata for individual assets (e.g., custom properties, registration info).
        *   UI interaction for managing resources is basic.
*   **"Export your mod to a build-ready Gradle project":**
    *   For both Forge and Fabric, the project creation process *results* in a Gradle project.
    *   There isn't a separate, distinct "export" function after project creation. The created project *is* the Gradle project.
    *   This could be clarified in documentation, but the core functionality (ending up with a Gradle project) is there.
*   **Data Page (`Views/Pages/DataPage.xaml` and `ViewModels/Pages/DataViewModel.cs`):**
    *   This page displays a large collection of colored blocks.
    *   Its relevance to Modrix's core functionality as a modding tool is unclear. It might be a remnant from a UI template or a test page and not an intended feature for the end-user in the context of modding.

# Not Working

*   **`Helpers/EmptyCollectionToVisibilityConverter.cs:17`:** `throw new NotImplementedException();` - Converter logic is not implemented.
*   **`Helpers/IntegerToVisibilityConverter.cs:22`:** `throw new NotImplementedException();` - Converter logic is not implemented.
*   **`Views/Windows/LoadingProjectWindow.xaml.cs:58`:** `// TODO: Implement stop functionality` - The ability to stop the project loading process is missing.
*   **`Views/Windows/MainWindow.xaml.cs:132`:** `throw new NotImplementedException();` - Likely related to an unimplemented UI interaction or feature.
*   **`Views/Windows/MainWindow.xaml.cs:137`:** `throw new NotImplementedException();` - Likely related to another unimplemented UI interaction or feature.
*   The `grep` output also included lines from `gradlew.bat` files (`Resources/Templates/Forge/Wrapper/gradlew.bat:19` and `Templates/FabricMod/gradlew.bat:19`). These are standard batch script lines (`@if "%DEBUG%"=="" @echo off`) and not indicative of bugs or incomplete features in the C#/.NET codebase. They can be ignored for this section.

# Planned for the Future

*   **NeoForge Support:**
    *   The README mentions NeoForge as a target mod loader, but current templating and project creation logic focuses on Forge and Fabric.
    *   This will require creating new project templates, updating the UI to include NeoForge as an option, and potentially new logic in `TemplateManager` or a dedicated `NeoForgeTemplateManager`.
*   **Microsoft Account Integration:**
    *   The README lists "Optional Microsoft account integration for Minecraft username/avatar display" as a feature.
    *   No specific code for this (e.g., authentication services, UI elements for login) has been observed yet.
*   **Enhanced Project Workspace (`WorkspacePage.xaml`):**
    *   Develop the currently placeholder "Workspace" page to provide meaningful interactions with the loaded project. This could include:
        *   Visual editors for common mod elements (e.g., blocks, items, entities).
        *   Configuration UIs for `mods.toml`, `fabric.mod.json`, or other metadata files post-creation.
        *   A more integrated file explorer/manager within the workspace.
*   **Full Language Control Functionality (`LanguageControlPage.xaml`):**
    *   Implement the "Language Control" page, presumably for managing localization files (`en_us.json`, etc.) for mods.
    *   This could involve UI for adding/editing translation keys and generating language files.
*   **Advanced Resource Management:**
    *   Model previewer (e.g., for `.obj` or `.gltf` if supported by Minecraft modding).
    *   Integrated editors for specific resource types (e.g., a simple texture painter, sound editor, or model property editor).
    *   Better metadata management for assets (e.g., defining custom properties, linking assets).
*   **Build & Run Integration:**
    *   Provide UI controls within Modrix to trigger common Gradle tasks (build, runClient, runServer, clean) directly from the workspace.
    *   Potentially integrate with IDEs for easier project opening.
*   **Debugging Tools:**
    *   Assistance with setting up debugging configurations for mods.
*   **Plugin/Addon System for Modrix:**
    *   Allow extending Modrix's functionality with community-developed plugins.
*   **More Mod Templates:**
    *   Provide a wider variety of starting templates (e.g., example block, example item, example entity) for different mod types.
*   **Comprehensive User Documentation & Tutorials:**
    *   Create detailed guides on how to use Modrix and develop mods with it.

# Roadmap

## Roadmap

This roadmap outlines the general direction for Modrix development, focusing on stabilizing current features, completing partially implemented ones, and then expanding with new capabilities.

**Phase 1: Stabilization & Core Functionality**

*   **Complete Fabric Project Generation:**
    *   Integrate automatic Gradle task execution (`genSources`, `genIntellijRuns`, or equivalent) for Fabric projects to match Forge parity for IDE setup.
    *   Thoroughly test and refine `JdkHelper` to ensure robust Java environment detection and guidance.
*   **Develop Core Workspace Features (`WorkspacePage.xaml`):**
    *   Implement essential functionalities for the main "Workspace" tab. This should at least include:
        *   A project overview/summary display.
        *   Easy access to edit key configuration files (e.g., `mods.toml`, `fabric.mod.json`) with syntax highlighting or basic forms.
        *   A simple file tree viewer for the project.
*   **Implement Language Control (`LanguageControlPage.xaml`):**
    *   Develop the UI and logic for managing basic language localization files (e.g., adding/editing key-value pairs for `en_us.json`).
*   **Refine Resource Manager:**
    *   Address any outstanding bugs or usability issues in the current resource manager.
    *   Improve error handling and feedback for file operations.
*   **Address "Not Working" Items:**
    *   Prioritize and fix issues identified in the "Not Working" section, particularly those impacting core functionality.

**Phase 2: Feature Expansion & User Experience**

*   **NeoForge Support:**
    *   Implement full project creation and management support for the NeoForge mod loader.
*   **Enhanced Resource Management:**
    *   Introduce model previews.
    *   Add basic integrated editors for some resource types if feasible.
*   **Build & Run Integration:**
    *   Allow triggering common Gradle tasks (build, runClient, runServer) from the Modrix UI (Console Page).
*   **Microsoft Account Integration:**
    *   Implement the planned Microsoft account integration for displaying Minecraft username/avatar.
*   **User Interface/User Experience (UI/UX) Polish:**
    *   Review and refine existing UI elements for consistency and ease of use.
    *   Improve visual feedback and error reporting throughout the application.

**Phase 3: Advanced Features & Community**

*   **Advanced Modding Tools:**
    *   Explore visual editors for specific mod elements (e.g., block properties, item creation wizards).
    *   Tools for easier management of data generation (e.g., recipes, tags, loot tables).
*   **Debugging Assistance:**
    *   Tools or guides to help users set up their development environment for debugging mods created with Modrix.
*   **More Project Templates:**
    *   Expand the library of built-in project templates for different types of mods or specific examples.
*   **Documentation & Tutorials:**
    *   Develop comprehensive user guides, tutorials, and API documentation (if applicable).
*   **Community Features (Long-Term):**
    *   Consider features like a plugin system for Modrix or integration with online modding communities/resources.

**Ongoing:**

*   **Bug Fixing:** Continuously address bugs and issues reported by users.
*   **Performance Optimization:** Monitor and improve the performance of the application.
*   **Code Quality:** Refactor and improve code quality for maintainability.
*   **Dependency Updates:** Keep dependencies (like .NET, WPF UI, Gradle versions) up to date.
