using System;
using System.Windows;
using System.Windows.Controls;
using chara2img.Models;
using chara2img.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

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
                LoadImageFromPath(dlg.FileName, imgInput);
            }
        }

        private void LoadImageButton_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && IsSupportedImage(files[0]))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void LoadImageButton_Drop(object sender, DragEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not ImageInput imgInput) return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var file = files?.FirstOrDefault();
                if (!string.IsNullOrEmpty(file) && IsSupportedImage(file))
                {
                    LoadImageFromPath(file, imgInput);
                }
            }
            e.Handled = true;
        }

        private void Category_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var file = files?.FirstOrDefault();
                // Accept only when the category contains an image input and the file is supported
                if (!string.IsNullOrEmpty(file) && IsSupportedImage(file) && sender is FrameworkElement fe && fe.DataContext is CategoryViewModel cvm && cvm.Inputs.OfType<ImageInput>().Any())
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Category_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                return;
            }

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var file = files?.FirstOrDefault();
            if (string.IsNullOrEmpty(file) || !IsSupportedImage(file))
            {
                e.Handled = true;
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is CategoryViewModel cvm)
            {
                var imgInput = cvm.Inputs.OfType<ImageInput>().FirstOrDefault();
                if (imgInput != null)
                {
                    LoadImageFromPath(file, imgInput);
                }
            }

            e.Handled = true;
        }

        private static bool IsSupportedImage(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        private void LoadImageFromPath(string path, ImageInput imgInput)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var prefix = "data:image/png;base64,";
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg") prefix = "data:image/jpeg;base64,";
                else if (ext == ".gif") prefix = "data:image/gif;base64,";
                else if (ext == ".bmp") prefix = "data:image/bmp;base64,";

                // Determine image dimensions
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    imgInput.ImageWidth = bmp.PixelWidth;
                    imgInput.ImageHeight = bmp.PixelHeight;
                }
                catch
                {
                    imgInput.ImageWidth = 0;
                    imgInput.ImageHeight = 0;
                }

                imgInput.Base64Data = prefix + Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not ImageInput imgInput) return;
            imgInput.Base64Data = "";
            imgInput.ImageWidth = 0;
            imgInput.ImageHeight = 0;
        }
    }
}