using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Modrix.ModElements
{
    /// <summary>
    /// Base class for all mod elements data
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ItemModElementData), typeDiscriminator: "item")]
    public abstract class ModElementData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconPath { get; set; } = "";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;

        [JsonIgnore]
        public virtual string Icon => IconPath;

        [JsonIgnore]
        public bool IsEdited { get; set; }

        public virtual void UpdateLastModified()
        {
            LastModified = DateTime.Now;
        }

        /// <summary>
        /// Serializes the mod element to JSON
        /// </summary>
        public string ToJson()
        {
            var options = new JsonSerializerOptions { 
                WriteIndented = true
            };
            return JsonSerializer.Serialize(this, options);
        }

        /// <summary>
        /// Deserializes a mod element from JSON
        /// </summary>
        public static ModElementData FromJson(string json)
        {
            var options = new JsonSerializerOptions();
            return JsonSerializer.Deserialize<ModElementData>(json, options);
        }
    }

    /// <summary>
    /// Data for an item mod element
    /// </summary>
    public class ItemModElementData : ModElementData
    {
        public string TexturePath { get; set; } = "";
        public string TranslationKey { get; set; } = "";
        public int MaxStackSize { get; set; } = 64;
        public bool HasGlint { get; set; } = false;
        public bool IsFood { get; set; } = false;
        public int FoodValue { get; set; } = 0;
        public float SaturationValue { get; set; } = 0;

        public ItemModElementData()
        {
            Type = "item";
        }
    }
}