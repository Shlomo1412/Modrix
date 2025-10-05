using System;
using System.IO;
using System.Windows.Controls;
using System.Windows;

namespace Modrix.Views.Pages
{
    public partial class ItemPickerDemoPage : Page
    {
        public ItemPickerDemoPage()
        {
            InitializeComponent();
        }

        private void DemoTexturePicker_TextureSelected(object sender, string? texturePath)
        {
            if (texturePath != null)
            {
                var fileName = Path.GetFileName(texturePath);
                TexturePickerStatus.Text = $"Selected: {fileName}";
            }
            else
            {
                TexturePickerStatus.Text = "No texture selected";
            }
        }

        private void DemoItemPicker_ItemSelected(object sender, string? itemPath)
        {
            if (itemPath != null)
            {
                var fileName = Path.GetFileName(itemPath);
                ItemPickerStatus.Text = $"Selected: {fileName}";
            }
            else
            {
                ItemPickerStatus.Text = "No item selected";
            }
        }

        private void ItemsOnlyPicker_ItemSelected(object sender, string? itemPath)
        {
            if (itemPath != null)
            {
                var fileName = Path.GetFileName(itemPath);
                ItemsOnlyStatus.Text = $"Item: {fileName}";
            }
            else
            {
                ItemsOnlyStatus.Text = "No item selected";
            }
        }

        private void BlocksOnlyPicker_ItemSelected(object sender, string? itemPath)
        {
            if (itemPath != null)
            {
                var fileName = Path.GetFileName(itemPath);
                BlocksOnlyStatus.Text = $"Block: {fileName}";
            }
            else
            {
                BlocksOnlyStatus.Text = "No block selected";
            }
        }

        private void BothPicker_ItemSelected(object sender, string? itemPath)
        {
            if (itemPath != null)
            {
                var fileName = Path.GetFileName(itemPath);
                BothStatus.Text = $"Selected: {fileName}";
            }
            else
            {
                BothStatus.Text = "No item/block selected";
            }
        }
    }
}