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
    }
}