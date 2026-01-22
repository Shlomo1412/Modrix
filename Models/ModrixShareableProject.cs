using System;
using System.Collections.Generic;

namespace Modrix.Models
{
    /// <summary>
    /// Represents a shareable .modrix project file format.
    /// This contains all project metadata and embedded resources needed to recreate the project.
    /// </summary>
    public class ModrixShareableProject
    {
        /// <summary>
        /// Format version for backwards compatibility
        /// </summary>
        public string FormatVersion { get; set; } = "1.0";

        /// <summary>
        /// Modrix application version that created this file
        /// </summary>
        public string ModrixVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Date when this shareable file was created
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Core project metadata
        /// </summary>
        public ShareableProjectMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Mod elements (items, blocks, etc.) - for mod projects only
        /// </summary>
        public List<ShareableModElement> ModElements { get; set; } = new();

        /// <summary>
        /// Embedded resources (textures, sounds, models, etc.)
        /// </summary>
        public List<EmbeddedResource> Resources { get; set; } = new();

        /// <summary>
        /// Custom code files that were modified by the user - for mod projects only
        /// </summary>
        public List<CustomCodeFile> CustomCode { get; set; } = new();

        /// <summary>
        /// Language/translation files
        /// </summary>
        public List<LanguageFile> Languages { get; set; } = new();

        /// <summary>
        /// Resource pack specific data - for resource pack projects only
        /// </summary>
        public ResourcePackShareableData? ResourcePackData { get; set; }

        /// <summary>
        /// Indicates if this is a resource pack project
        /// </summary>
        public bool IsResourcePack => Metadata?.ModType?.Contains("Resource Pack", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Core project metadata needed to recreate the project
    /// </summary>
    public class ShareableProjectMetadata
    {
        public string Name { get; set; } = "";
        public string ModId { get; set; } = "";
        public string Package { get; set; } = "";
        public string ModType { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public string Description { get; set; } = "";
        public string Authors { get; set; } = "";
        public string License { get; set; } = "";
        public string ModVersion { get; set; } = "1.0.0";
        public bool IncludeReadme { get; set; }
    }

    /// <summary>
    /// Represents a mod element in the shareable format
    /// </summary>
    public class ShareableModElement
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>
        /// JSON serialized element data
        /// </summary>
        public string Data { get; set; } = "";
    }

    /// <summary>
    /// Represents an embedded resource file (texture, sound, model, etc.)
    /// </summary>
    public class EmbeddedResource
    {
        /// <summary>
        /// Relative path within the project's assets folder
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// Resource type: texture, sound, model, animation, icon
        /// </summary>
        public string ResourceType { get; set; } = "";

        /// <summary>
        /// Base64 encoded file content
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Original file size in bytes (for validation)
        /// </summary>
        public long OriginalSize { get; set; }
    }

    /// <summary>
    /// Represents a custom code file modified by the user
    /// </summary>
    public class CustomCodeFile
    {
        /// <summary>
        /// Relative path from project root
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// The actual code content
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// File type: java, json, properties, etc.
        /// </summary>
        public string FileType { get; set; } = "";

        /// <summary>
        /// Whether this is a completely custom file (not from template)
        /// </summary>
        public bool IsCustomFile { get; set; }

        /// <summary>
        /// Type of modification detected
        /// </summary>
        public CodeModificationType ModificationType { get; set; } = CodeModificationType.Unknown;

        /// <summary>
        /// Original template file path this was based on (if modified from template)
        /// </summary>
        public string? OriginalTemplatePath { get; set; }
    }

    /// <summary>
    /// Type of code modification
    /// </summary>
    public enum CodeModificationType
    {
        /// <summary>
        /// Unknown modification status
        /// </summary>
        Unknown,

        /// <summary>
        /// File is completely new (not from any template)
        /// </summary>
        NewFile,

        /// <summary>
        /// File was modified from the original template
        /// </summary>
        Modified,

        /// <summary>
        /// File matches the expected template output (no user changes)
        /// </summary>
        Unmodified
    }

    /// <summary>
    /// Represents a language/translation file
    /// </summary>
    public class LanguageFile
    {
        /// <summary>
        /// Language code (e.g., en_us, es_es)
        /// </summary>
        public string LanguageCode { get; set; } = "";

        /// <summary>
        /// JSON content of the language file
        /// </summary>
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// Resource pack specific shareable data
    /// </summary>
    public class ResourcePackShareableData
    {
        /// <summary>
        /// Pack format version
        /// </summary>
        public int PackFormat { get; set; }

        /// <summary>
        /// Texture overrides (relative path -> base64 content)
        /// </summary>
        public List<ResourcePackOverride> TextureOverrides { get; set; } = new();

        /// <summary>
        /// Model overrides
        /// </summary>
        public List<ResourcePackOverride> ModelOverrides { get; set; } = new();

        /// <summary>
        /// Sound overrides
        /// </summary>
        public List<ResourcePackOverride> SoundOverrides { get; set; } = new();

        /// <summary>
        /// Translation overrides
        /// </summary>
        public List<ResourcePackOverride> TranslationOverrides { get; set; } = new();

        /// <summary>
        /// Pack icon (base64 encoded)
        /// </summary>
        public string? PackIcon { get; set; }

        /// <summary>
        /// Pack.mcmeta content
        /// </summary>
        public string? PackMcmeta { get; set; }
    }

    /// <summary>
    /// Represents a resource pack override file
    /// </summary>
    public class ResourcePackOverride
    {
        /// <summary>
        /// Relative path within the resource pack
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// Category (block, item, entity, gui, etc.)
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Base64 encoded content (for binary files) or plain text (for JSON)
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Whether content is base64 encoded
        /// </summary>
        public bool IsBase64 { get; set; } = true;

        /// <summary>
        /// Original file size
        /// </summary>
        public long OriginalSize { get; set; }
    }
}
