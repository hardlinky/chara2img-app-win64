using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using chara2img.Models;

namespace chara2img.Views
{
    /// <summary>
    /// Interaction logic for RunTab.xaml
    /// </summary>
    public partial class RunTab
    {
        private static bool _dontAskAgain = false; // Session-only flag (resets on app restart)

        public RunTab()
        {
            InitializeComponent();
        }

        private void JobsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (JobsListView.SelectedItem is RunpodJob job && !string.IsNullOrEmpty(job.Id))
            {
                try
                {
                    System.Windows.Clipboard.SetText(job.Id);
                    
                    // Optional: Show a brief confirmation
                    if (DataContext is ViewModels.MainViewModel viewModel)
                    {
                        var previousStatus = viewModel.StatusMessage;
                        viewModel.StatusMessage = $"Job ID copied to clipboard: {job.Id}";
                        
                        // Reset status after 2 seconds
                        System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (viewModel.StatusMessage.StartsWith("Job ID copied"))
                                {
                                    viewModel.StatusMessage = previousStatus;
                                }
                            });
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", 
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void JobsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && JobsListView.SelectedItems.Count > 0)
            {
                if (DataContext is ViewModels.MainViewModel viewModel)
                {
                    // Get all selected jobs
                    var selectedJobs = JobsListView.SelectedItems.Cast<RunpodJob>().ToList();
                    
                    if (selectedJobs.Count == 0)
                        return;

                    bool shouldDelete = true;

                    // Show confirmation dialog if "Don't ask again" is not checked
                    if (!_dontAskAgain)
                    {
                        shouldDelete = ShowDeleteConfirmation(selectedJobs.Count);
                    }

                    if (shouldDelete)
                    {
                        // Remove all selected jobs
                        foreach (var job in selectedJobs)
                        {
                            if (viewModel.SelectedJob == job)
                            {
                                viewModel.SelectedJob = null;
                            }
                            viewModel.Jobs.Remove(job);
                        }

                        // Update status message
                        var statusMsg = selectedJobs.Count == 1
                            ? $"Removed job {selectedJobs[0].Id}"
                            : $"Removed {selectedJobs.Count} jobs";
                        
                        viewModel.StatusMessage = statusMsg;
                    }
                }

                e.Handled = true;
            }
        }

        private bool ShowDeleteConfirmation(int jobCount)
        {
            // Create a custom dialog window
            var dialog = new Window
            {
                Title = "Confirm Removal",
                Width = 450,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = Application.Current.Resources["WindowBackgroundBrush"] as System.Windows.Media.Brush
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Message
            var message = jobCount == 1
                ? "Are you sure you want to remove this job?\n\nThis will not delete the generated images from disk."
                : $"Are you sure you want to remove {jobCount} selected jobs?\n\nThis will not delete the generated images from disk.";

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = Application.Current.Resources["TextBrush"] as System.Windows.Media.Brush
            };
            Grid.SetRow(messageBlock, 0);
            grid.Children.Add(messageBlock);

            // "Don't ask again" checkbox
            var dontAskCheckBox = new CheckBox
            {
                Content = "Don't ask again this session",
                Margin = new Thickness(0, 0, 0, 20),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = Application.Current.Resources["TextBrush"] as System.Windows.Media.Brush
            };
            Grid.SetRow(dontAskCheckBox, 1);
            grid.Children.Add(dontAskCheckBox);

            // Spacer
            Grid.SetRow(new Border(), 2);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var yesButton = new Button
            {
                Content = "Yes",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            yesButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };

            var noButton = new Button
            {
                Content = "No",
                Width = 80,
                Height = 30,
                IsCancel = true
            };
            noButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            var result = dialog.ShowDialog() ?? false;

            // Update the session flag if checkbox was checked
            if (dontAskCheckBox.IsChecked == true)
            {
                _dontAskAgain = true;
            }

            return result;
        }
    }
}