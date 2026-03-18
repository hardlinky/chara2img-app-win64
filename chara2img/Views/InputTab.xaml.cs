using System;
using System.Windows;
using System.Windows.Controls;
using chara2img.Models;
using chara2img.ViewModels;
using Microsoft.Win32;
using System.IO;

namespace chara2img.Views
{
    /// <summary>
    /// Interaction logic for InputTab.xaml
    /// </summary>
    public partial class InputTab
    {
        public InputTab()
        {
            InitializeComponent();
        }

        private void NameField_LostFocus(object sender, RoutedEventArgs e)
        {
            HandleNameFieldUpdate(sender);
        }

        private void NameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleNameFieldUpdate(sender);
        }

        private void HandleNameFieldUpdate(object sender)
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not WorkflowInput input) return;
            if (DataContext is not MainViewModel viewModel) return;

            // Only handle Name fields in specific categories
            if (!input.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase)) return;
            if (input.Category != "Character" && input.Category != "Costume" && input.Category != "Character Pose") return;

            // Execute the update command
            if (viewModel.UpdateNamedVariableHintsCommand?.CanExecute(input.Category) == true)
            {
                viewModel.UpdateNamedVariableHintsCommand.Execute(input.Category);
            }
        }

        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not ImageInput imgInput) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bytes = File.ReadAllBytes(dlg.FileName);
                    var prefix = "data:image/png;base64,";
                    // Try to detect png/jpg
                    var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg") prefix = "data:image/jpeg;base64,";
                    else if (ext == ".gif") prefix = "data:image/gif;base64,";
                    else if (ext == ".bmp") prefix = "data:image/bmp;base64,";

                    imgInput.Base64Data = prefix + Convert.ToBase64String(bytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load image:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not ImageInput imgInput) return;
            imgInput.Base64Data = "";
        }
    }
}