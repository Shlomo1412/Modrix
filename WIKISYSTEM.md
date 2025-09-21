# WikiTooltip System Documentation

## Overview

The WikiTooltip system provides an easy way to add contextual help throughout the Modrix application. Each WikiTooltip shows helpful information when hovered and automatically contributes to an in-app knowledge base.

## Features

- **Easy Integration**: Simply add `<controls:WikiTooltip>` to any XAML element
- **Automatic Wiki Generation**: All tooltips automatically appear in the Wiki page
- **Categorization**: Organize entries by category (Models, Textures, Tools, etc.)
- **Search Support**: Users can search the wiki by title, description, or keywords
- **Consistent Styling**: Custom styled tooltips that match the app theme

## Basic Usage

### 1. Add Namespace to XAML
```xaml
xmlns:controls="clr-namespace:Modrix.Views.Controls"
```

### 2. Add WikiTooltip to UI Elements
```xaml
<StackPanel Orientation="Horizontal">
    <TextBlock Text="Texture Mappings" FontSize="22" FontWeight="Bold"/>
    <controls:WikiTooltip Margin="8,0,0,0"
                        VerticalAlignment="Center"
                        WikiId="texture-mappings"
                        Title="What are Texture Mappings?"
                        Category="Models"
                        Keywords="textures,models,mapping,references,paths"
                        Description="Texture mappings define which texture files your 3D models should use. When a model references a texture that doesn't exist in your project, it creates a 'missing mapping'."/>
</StackPanel>
```

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `WikiId` | string | Yes | Unique identifier for the wiki entry |
| `Title` | string | Yes | Title displayed in the tooltip and wiki |
| `Category` | string | Yes | Category for organization (Models, Textures, Tools, General, Projects) |
| `Description` | string | Yes | Detailed explanation of the concept |
| `Keywords` | string | No | Comma-separated keywords for search functionality |

## Categories

Use these standard categories for consistency:

- **Models**: Information about 3D models, JSON files, and Blockbench
- **Textures**: Texture files, formats, and image editing
- **Projects**: Project management and workspace concepts
- **Tools**: Development tools, build processes, and utilities
- **General**: Application features and general concepts

## Using WikiHelper

For programmatic creation, use the `WikiHelper` class:

```csharp
using Modrix.Helpers;

// Create a tooltip
var tooltip = WikiHelper.CreateTooltip(
    "my-concept",
    "My Concept",
    "General",
    "This explains my concept in detail.",
    "keyword1,keyword2,keyword3"
);

// Add to a StackPanel
var panel = new StackPanel();
WikiHelper.AddTooltipToPanel(panel, "my-concept", "My Concept", "General", "Description");

// Use common predefined entries
var projectTooltip = WikiHelper.CommonEntries.ProjectManagement;
```

## Best Practices

1. **Use Descriptive IDs**: Make WikiId descriptive but unique (e.g., "texture-mappings", not "tooltip1")

2. **Choose Appropriate Categories**: Use existing categories when possible to keep the wiki organized

3. **Write Clear Descriptions**: Explain concepts as if the user is new to modding

4. **Add Relevant Keywords**: Include synonyms and related terms for better searchability

5. **Keep Tooltips Concise**: The tooltip should provide quick help; detailed information goes in the wiki

6. **Test Tooltip Placement**: Ensure tooltips don't interfere with UI interaction

## Examples in Codebase

See these files for examples:
- `Views\Windows\MissingMappingsDialog.xaml` - Texture mappings explanation
- `Views\Pages\ResourcesPage.xaml` - Texture files, model validation, and README info
- `Views\Pages\SettingsPage.xaml` - Java JDK explanation

## Accessing the Wiki

Users can access the complete wiki through:
1. **Navigation Menu**: The Wiki item in the main window footer
2. **Search Functionality**: Search all entries by keywords or content
3. **Category Browsing**: Organized by categories for easy navigation

## Technical Details

- **Service**: `WikiService` manages all wiki entries as a singleton
- **Models**: `WikiEntry` and `WikiCategory` define the data structure
- **Auto-registration**: Tooltips automatically register when `WikiId` is set
- **Thread-safe**: The service can be accessed from any thread
- **Persistent**: Entries remain available throughout the application session

This system makes it easy to provide contextual help while building a comprehensive knowledge base for users.