using System;
using System.Windows;
using System.Windows.Controls;
using chara2img.Models;
using chara2img.ViewModels;

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
    }
}