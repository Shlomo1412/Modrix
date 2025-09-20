# 📦 NuGet Packages Used in Modrix

This document provides a comprehensive overview of all NuGet packages used in the Modrix project, including their descriptions, licenses, authors, and purposes within the application.

---

## 📋 Package Overview

| Package | Version | License | Author |
|---------|---------|---------|---------|
| [AvalonEdit](#avalonedit) | 6.3.1.120 | MIT | AvalonEdit Team |
| [HelixToolkit.Wpf](#helixtoolkitwpf) | 2.27.3 | MIT | Helix Toolkit Contributors |
| [LibGit2Sharp](#libgit2sharp) | 0.31.0 | MIT | LibGit2Sharp Team |
| [PixiEditor.ColorPicker](#pixieditorcolorpicker) | 3.4.2 | MIT | PixiEditor Team |
| [System.Management](#systemmanagement) | 9.0.6 | MIT | Microsoft |
| [WPF-UI](#wpf-ui) | 4.0.2 | MIT | Lepo.co |
| [WPF-UI.DependencyInjection](#wpf-ui-dependencyinjection) | 4.0.2 | MIT | Lepo.co |
| [Microsoft.Extensions.Hosting](#microsoftextensionshosting) | 9.0.1 | MIT | Microsoft |
| [CommunityToolkit.Mvvm](#communitytoolkitmvvm) | 8.2.2 | MIT | .NET Foundation |
| [Scriban](#scriban) | 5.0.0 | BSD-2-Clause | Alexandre Mutel |
| [WPF-UI.Markdown](#wpf-ui-markdown) | 4.0.2 | MIT | Lepo.co |

---

## 📖 Detailed Package Information

### AvalonEdit
> **Advanced Text Editor Component for WPF**

- **📍 NuGet Link**: [AvalonEdit on NuGet](https://www.nuget.org/packages/AvalonEdit/)
- **🌐 Official Repository**: [AvalonEdit on GitHub](https://github.com/icsharpcode/AvalonEdit)
- **👥 Author**: AvalonEdit Team (ICSharpCode)
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Powers the code editor functionality for editing Java mod source files, configuration files, and other text-based content within the IDE workspace.

**Key Features:**
- Syntax highlighting for multiple languages
- Code folding and line numbering  
- Find/Replace functionality
- Undo/Redo support
- IntelliSense-like features

---

### HelixToolkit.Wpf
> **High-Performance 3D Graphics Toolkit for WPF**

- **📍 NuGet Link**: [HelixToolkit.Wpf on NuGet](https://www.nuget.org/packages/HelixToolkit.Wpf/)
- **🌐 Official Repository**: [HelixToolkit on GitHub](https://github.com/helix-toolkit/helix-toolkit)
- **👥 Author**: Helix Toolkit Contributors
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Enables 3D model viewing and manipulation capabilities for Minecraft mod assets, including block models, item models, and entity models.

**Key Features:**
- 3D model rendering and manipulation
- Camera controls and viewport management
- Material and lighting support
- Export and import capabilities for various 3D formats

---

### LibGit2Sharp
> **Git Operations Library for .NET**

- **📍 NuGet Link**: [LibGit2Sharp on NuGet](https://www.nuget.org/packages/LibGit2Sharp/)
- **🌐 Official Repository**: [LibGit2Sharp on GitHub](https://github.com/libgit2/libgit2sharp)
- **👥 Author**: LibGit2Sharp Team
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Provides version control integration, allowing users to initialize Git repositories for their mod projects, commit changes, and manage project versioning directly from the IDE.

**Key Features:**
- Repository initialization and cloning
- Commit and branch management
- Diff and merge operations
- Remote repository operations

---

### PixiEditor.ColorPicker
> **Advanced Color Picker Control for WPF**

- **📍 NuGet Link**: [PixiEditor.ColorPicker on NuGet](https://www.nuget.org/packages/PixiEditor.ColorPicker/)
- **🌐 Official Repository**: [PixiEditor on GitHub](https://github.com/PixiEditor/PixiEditor)
- **👥 Author**: PixiEditor Team
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Used in the texture editor for precise color selection when creating and editing Minecraft textures, providing professional-grade color picking capabilities.

**Key Features:**
- HSV and RGB color space support
- Eyedropper tool functionality
- Color palette management
- Hex color code input/output

---

### System.Management
> **System Management APIs for .NET**

- **📍 NuGet Link**: [System.Management on NuGet](https://www.nuget.org/packages/System.Management/)
- **🌐 Official Documentation**: [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/api/system.management)
- **👥 Author**: Microsoft Corporation
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Provides system-level access for detecting Java installations, managing system processes, and gathering system information required for mod compilation and testing.

**Key Features:**
- WMI (Windows Management Instrumentation) access
- System process management
- Hardware and software inventory
- System configuration retrieval

---

### WPF-UI
> **Modern Fluent Design System for WPF**

- **📍 NuGet Link**: [WPF-UI on NuGet](https://www.nuget.org/packages/WPF-UI/)
- **🌐 Official Repository**: [WPF-UI on GitHub](https://github.com/lepoco/wpfui)
- **👥 Author**: Lepo.co (Leszek Pomianowski)
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Provides the modern, Fluent Design-based UI framework that gives Modrix its contemporary look and feel, including navigation, controls, and theming.

**Key Features:**
- Fluent Design System implementation
- Dark/Light theme support
- Modern control library
- Navigation and layout components
- Mica and Acrylic effects

---

### WPF-UI.DependencyInjection
> **Dependency Injection Extension for WPF-UI**

- **📍 NuGet Link**: [WPF-UI.DependencyInjection on NuGet](https://www.nuget.org/packages/WPF-UI.DependencyInjection/)
- **🌐 Official Repository**: [WPF-UI on GitHub](https://github.com/lepoco/wpfui)
- **👥 Author**: Lepo.co (Leszek Pomianowski)
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Enables dependency injection integration with WPF-UI components, allowing for clean architecture patterns and better testability in the application.

**Key Features:**
- Service registration and resolution
- ViewModel injection
- Page and window service management
- Integration with Microsoft.Extensions.DependencyInjection

---

### Microsoft.Extensions.Hosting
> **Application Hosting Abstractions and Default Implementation**

- **📍 NuGet Link**: [Microsoft.Extensions.Hosting on NuGet](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/)
- **🌐 Official Documentation**: [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/core/extensions/hosting)
- **👥 Author**: Microsoft Corporation
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Provides the application hosting framework, enabling proper application lifecycle management, dependency injection, and service hosting capabilities.

**Key Features:**
- Application lifecycle management
- Configuration management
- Logging infrastructure
- Background service hosting
- Graceful shutdown handling

---

### CommunityToolkit.Mvvm
> **MVVM Toolkit for .NET Applications**

- **📍 NuGet Link**: [CommunityToolkit.Mvvm on NuGet](https://www.nuget.org/packages/CommunityToolkit.Mvvm/)
- **🌐 Official Repository**: [Windows Community Toolkit](https://github.com/CommunityToolkit/WindowsCommunityToolkit)
- **👥 Author**: .NET Foundation
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Provides MVVM infrastructure including observable objects, relay commands, and messaging, enabling clean separation of concerns between UI and business logic.

**Key Features:**
- Source generators for MVVM boilerplate
- ObservableObject and ObservableProperty
- RelayCommand implementations
- Messenger for loose coupling
- Dependency injection integration

---

### Scriban
> **Fast, Powerful, Safe and Lightweight Scripting Language and Template Engine**

- **📍 NuGet Link**: [Scriban on NuGet](https://www.nuget.org/packages/Scriban/)
- **🌐 Official Repository**: [Scriban on GitHub](https://github.com/scriban/scriban)
- **👥 Author**: Alexandre Mutel
- **📜 License**: BSD-2-Clause License
- **🎯 Purpose in Modrix**: Powers the template engine for generating mod project structures, Java class templates, JSON configurations, and other file templates when creating new mods.

**Key Features:**
- Liquid-compatible template syntax
- Safe scripting environment
- High performance rendering
- Extensible with custom functions
- Support for complex data models

---

### WPF-UI.Markdown
> **Markdown Rendering Component for WPF-UI**

- **📍 NuGet Link**: [WPF-UI.Markdown on NuGet](https://www.nuget.org/packages/WPF-UI.Markdown/)
- **🌐 Official Repository**: [WPF-UI on GitHub](https://github.com/lepoco/wpfui)
- **👥 Author**: Lepo.co (Leszek Pomianowski)
- **📜 License**: MIT License
- **🎯 Purpose in Modrix**: Enables markdown rendering capabilities within the application, used for displaying formatted documentation, README files, and help content with proper styling.

**Key Features:**
- CommonMark specification compliance
- Syntax highlighting for code blocks
- Table and link support
- Integration with WPF-UI theming
- Custom renderer extensions

---

## 🔍 License Summary

| License Type | Count | Packages |
|-------------|--------|----------|
| MIT License | 10 | AvalonEdit, HelixToolkit.Wpf, LibGit2Sharp, PixiEditor.ColorPicker, System.Management, WPF-UI, WPF-UI.DependencyInjection, Microsoft.Extensions.Hosting, CommunityToolkit.Mvvm, WPF-UI.Markdown |
| BSD-2-Clause | 1 | Scriban |

---

## ⚖️ License Compliance

All packages used in Modrix are distributed under permissive open-source licenses (MIT and BSD-2-Clause) that allow:

- ✅ Commercial use
- ✅ Modification
- ✅ Distribution
- ✅ Private use

**Requirements:**
- 📄 Include original license text (see `/licenses` directory)
- 📄 Include copyright notices
- ⚠️ No warranty disclaimers apply

---

## 📁 License Files

All license files for the packages are available in the [`/licenses`](./licenses/) directory for compliance and reference purposes.

---

**Last Updated**: December 2024  
**Modrix Version**: Current  
**Total Packages**: 11