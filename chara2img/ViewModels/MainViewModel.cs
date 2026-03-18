using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using chara2img.Models;
using chara2img.Services;
using System.Text.Json;

namespace chara2img.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private RunpodService? _runpodService;
        private string _apiKey = "";
        private string _endpointId = "";
        private string _statusMessage = "Ready";
        private bool _isRunning = false;
        private BitmapImage? _currentImage;
        private string _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Chara2IMG");
        private string _workflowJson = "";
        private string _workflowFilePath = "";
        private readonly AppSettings _settings;
        private RunpodJob? _selectedJob;
        private string _selectedJobStatusResponse = "";
        private bool _isStatusResponseExpanded = false;
        private bool _isGalleryView = true;
        private double _imageZoom = 1.0;
        private const int MaxRecentJobs = 50; // Keep last 50 jobs
        private string _selectedTheme = "Light";
        private CancellationTokenSource? _saveCategoryCts;
        private readonly object _saveLock = new();

        private int _maxPollingAttempts = 100;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<RunpodJob> Jobs { get; } = new();
        public ObservableCollection<BitmapImage> CurrentImages { get; } = new();
        public ObservableCollection<string> AvailableThemes { get; } = new() { "Light", "Dark" };
        public ObservableCollection<string> ActiveJobStatuses { get; } = new();

        private ObservableCollection<CategoryInfo> _categories = new();

        public ObservableCollection<CategoryInfo> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); }
        }

        // Add this new property for the pre-built category UI items
        private ObservableCollection<CategoryViewModel> _categoryViewModels = new();

        private ObservableCollection<CategoryViewModel> _primaryViewModels = new();
        private ObservableCollection<CategoryViewModel> _secondaryViewModels = new();

        public ObservableCollection<CategoryViewModel> PrimaryViewModels
        {
            get => _primaryViewModels;
            set { _primaryViewModels = value; OnPropertyChanged(); }
        }

        public ObservableCollection<CategoryViewModel> SecondaryViewModels
        {
            get => _secondaryViewModels;
            set { _secondaryViewModels = value; OnPropertyChanged(); }
        }

        public bool HasSecondaryCategories => SecondaryViewModels.Count > 0;

        public ObservableCollection<CategoryViewModel> CategoryViewModels
        {
            get => _categoryViewModels;
            set { _categoryViewModels = value; OnPropertyChanged(); }
        }

        public string ApiKey
        {
            get => _apiKey;
            set 
            {
                if (_apiKey != value)
                {
                    _apiKey = value;
                    _runpodService = null; // Invalidate service when credentials change
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string EndpointId
        {
            get => _endpointId;
            set 
            {
                if (_endpointId != value)
                {
                    _endpointId = value;
                    _runpodService = null; // Invalidate service when credentials change
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        public BitmapImage? CurrentImage
        {
            get => _currentImage;
            set { _currentImage = value; OnPropertyChanged(); }
        }

        public bool IsGalleryView
        {
            get => _isGalleryView;
            set { _isGalleryView = value; OnPropertyChanged(); }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set 
            { 
                _outputFolder = value; 
                OnPropertyChanged();
                SaveSettings();
            }
        }

        private bool _isInitialLoad = true;

        public string WorkflowJson
        {
            get => _workflowJson;
            set
            {
                _workflowJson = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWorkflow));
                ParseWorkflowInputs();
            }
        }

        public string WorkflowFilePath
        {
            get => _workflowFilePath;
            set 
            { 
                _workflowFilePath = value; 
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public RunpodJob? SelectedJob
        {
            get => _selectedJob;
            set 
            { 
                _selectedJob = value;
                OnPropertyChanged();
                UpdateSelectedJobStatus();
                LoadJobImages();
            }
        }

        public string SelectedJobStatusResponse
        {
            get => _selectedJobStatusResponse;
            set { _selectedJobStatusResponse = value; OnPropertyChanged(); }
        }

        public bool IsStatusResponseExpanded
        {
            get => _isStatusResponseExpanded;
            set { _isStatusResponseExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusResponsePreview)); }
        }

        public string StatusResponsePreview
        {
            get
            {
                if (string.IsNullOrEmpty(_selectedJobStatusResponse))
                    return _selectedJobStatusResponse;

                if (_isStatusResponseExpanded)
                {
                    // When expanded, show full JSON without any base64 truncation
                    return _selectedJobStatusResponse;
                }
                else
                {
                    // When collapsed: FIRST truncate base64, THEN limit to 500 chars
                    var withTruncatedBase64 = RunpodService.TruncateBase64InJson(_selectedJobStatusResponse, removeCompletely: true);
                    
                    // Now limit the overall length
                    if (withTruncatedBase64.Length > 500)
                    {
                        return withTruncatedBase64.Substring(0, 500) + "...\n\n[Click 'Show Full Response' to see more]";
                    }
                    
                    return withTruncatedBase64;
                }
            }
        }

        public bool HasWorkflow => !string.IsNullOrWhiteSpace(_workflowJson);

        private Dictionary<string, ObservableCollection<WorkflowInput>> _workflowInputs = new();

        public Dictionary<string, ObservableCollection<WorkflowInput>> WorkflowInputs
        {
            get => _workflowInputs;
            set
            {
                _workflowInputs = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                    ApplyTheme(value);
                    SaveSettings();
                }
            }
        }

        public int MaxPollingAttempts
        {
            get => _maxPollingAttempts;
            set 
            { 
                if (_maxPollingAttempts != value && value > 0)
                {
                    _maxPollingAttempts = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public ICommand RunJobCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand LoadWorkflowCommand { get; }
        public ICommand ValidateWorkflowCommand { get; }
        public ICommand RefreshJobStatusCommand { get; }
        public ICommand ToggleStatusResponseCommand { get; }
        public ICommand OpenImageFolderCommand { get; }
        public ICommand ShowImageCommand { get; }
        public ICommand CloseImageCommand { get; }
        public ICommand SaveImageAsCommand { get; }
        public ICommand CancelOrRemoveJobCommand { get; }
        public ICommand RerunJobCommand { get; }
        public ICommand LoadJobInputsCommand { get; }

        // Add new command for navigating jobs
        public ICommand NavigateToPreviousJobCommand { get; }
        public ICommand NavigateToNextJobCommand { get; }

        public ICommand MoveCategoryUpCommand { get; }
        public ICommand MoveCategoryDownCommand { get; }
        public ICommand ToggleCategoryCommand { get; }
        public ICommand MoveCategoryToOtherViewCommand { get; }

        public ICommand AddLoraCommand { get; }
        public ICommand RemoveLoraCommand { get; }
        
        public ICommand CopyToClipboardCommand { get; }
        public ICommand UpdateNamedVariableHintsCommand { get; }

        public double ImageZoom
        {
            get => _imageZoom;
            set { _imageZoom = value; OnPropertyChanged(); }
        }

        private bool _saveWorkflowWithJob = true;

        public bool SaveWorkflowWithJob
        {
            get => _saveWorkflowWithJob;
            set 
            { 
                if (_saveWorkflowWithJob != value)
                {
                    _saveWorkflowWithJob = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        private int _selectedTabIndex = 0;

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set 
            { 
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isFullscreen = false;

        public bool IsFullscreen
        {
            get => _isFullscreen;
            set { _isFullscreen = value; OnPropertyChanged(); }
        }

        public ICommand ToggleFullscreenCommand { get; }

        public MainViewModel()
        {
            RunJobCommand = new RelayCommand(async () => await RunJobAsync(), CanRunJob);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            LoadWorkflowCommand = new RelayCommand(LoadWorkflowFile);
            ValidateWorkflowCommand = new RelayCommand(ValidateWorkflow);
            RefreshJobStatusCommand = new RelayCommand(async () => await RefreshJobStatusAsync(), () => _selectedJob != null && !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_endpointId));
            ToggleStatusResponseCommand = new RelayCommand(() => IsStatusResponseExpanded = !IsStatusResponseExpanded);
            OpenImageFolderCommand = new RelayCommand(OpenImageFolder);
            ShowImageCommand = new RelayCommand<BitmapImage>(ShowImage);
            CloseImageCommand = new RelayCommand(() => { IsGalleryView = true; CurrentImage = null; });
            SaveImageAsCommand = new RelayCommand(SaveImageAs, () => CurrentImage != null && !IsGalleryView);
            CancelOrRemoveJobCommand = new RelayCommand<RunpodJob>(async job => await CancelOrRemoveJobAsync(job), job => job != null);
            RerunJobCommand = new RelayCommand<RunpodJob>(async job => await RerunJobAsync(job), job => job != null && !string.IsNullOrEmpty(job?.WorkflowInputsJson) && !IsRunning);
            LoadJobInputsCommand = new RelayCommand<RunpodJob>(LoadJobInputs, job => job != null && !string.IsNullOrEmpty(job?.WorkflowInputsJson));
            NavigateToPreviousJobCommand = new RelayCommand(NavigateToPreviousJob, CanNavigateToPreviousJob);
            NavigateToNextJobCommand = new RelayCommand(NavigateToNextJob, CanNavigateToNextJob);

            MoveCategoryUpCommand = new RelayCommand<CategoryInfo>(MoveCategoryUp, CanMoveCategoryUp);
            MoveCategoryDownCommand = new RelayCommand<CategoryInfo>(MoveCategoryDown, CanMoveCategoryDown);
            ToggleCategoryCommand = new RelayCommand<CategoryInfo>(ToggleCategory);
            MoveCategoryToOtherViewCommand = new RelayCommand<CategoryInfo>(MoveCategoryToOtherView);

            AddLoraCommand = new RelayCommand<object>(AddLora);
            RemoveLoraCommand = new RelayCommand<LoraItem>(RemoveLora, item => item != null);
            
            CopyToClipboardCommand = new RelayCommand<string>(CopyToClipboard, text => !string.IsNullOrEmpty(text));
            UpdateNamedVariableHintsCommand = new RelayCommand<string>(UpdateNamedVariableHints);

            ToggleFullscreenCommand = new RelayCommand(ToggleFullscreen, () => !IsGalleryView && CurrentImage != null);

            // Load settings
            _settings = AppSettings.Load();
            _apiKey = _settings.ApiKey;
            _endpointId = _settings.EndpointId;
            _selectedTheme = _settings.Theme;
            _maxPollingAttempts = _settings.MaxPollingAttempts;

            if (!string.IsNullOrEmpty(_settings.OutputFolder))
            {
                _outputFolder = _settings.OutputFolder;
            }

            if (!string.IsNullOrEmpty(_settings.LastWorkflowPath) && File.Exists(_settings.LastWorkflowPath))
            {
                try
                {
                    _workflowFilePath = _settings.LastWorkflowPath;
                    _workflowJson = File.ReadAllText(_settings.LastWorkflowPath);
                    // Parse inputs after loading workflow
                    ParseWorkflowInputs();
                }
                catch
                {
                    // Ignore if we can't load the last workflow
                }
            }

            // Load recent jobs
            if (_settings.RecentJobs != null && _settings.RecentJobs.Count > 0)
            {
                foreach (var job in _settings.RecentJobs)
                {
                    Jobs.Add(job);
                }
            }

            // Create output folder if it doesn't exist
            Directory.CreateDirectory(_outputFolder);

            // Apply saved theme
            ApplyTheme(_selectedTheme);

            // Notify all properties to update UI with loaded values
            OnPropertyChanged(nameof(ApiKey));
            OnPropertyChanged(nameof(EndpointId));
            OnPropertyChanged(nameof(OutputFolder));
            OnPropertyChanged(nameof(WorkflowFilePath));
            OnPropertyChanged(nameof(WorkflowJson));
            OnPropertyChanged(nameof(HasWorkflow));
            OnPropertyChanged(nameof(WorkflowInputs)); // Add this to ensure inputs are updated
            OnPropertyChanged(nameof(SaveWorkflowWithJob));
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(MaxPollingAttempts));
        }

        private void MoveCategoryToOtherView(CategoryInfo? category)
        {
            if (category == null) return;

            // Toggle the view
            category.ViewIndex = category.ViewIndex == 0 ? 1 : 0;

            // After moving, check if we need to swap views
            var primaryCategories = Categories.Where(c => c.ViewIndex == 0).ToList();
            var secondaryCategories = Categories.Where(c => c.ViewIndex == 1).ToList();

            // If primary is now empty but secondary has items, swap them all back to primary
            if (primaryCategories.Count == 0 && secondaryCategories.Count > 0)
            {
                foreach (var cat in Categories)
                {
                    cat.ViewIndex = 0;
                }
            }

            // Rebuild view models and save
            BuildCategoryViewModels();
            SaveCategoryPreferencesDebounced();
        }

        private void ToggleFullscreen()
        {
            IsFullscreen = !IsFullscreen;
        }

        private void ApplyTheme(string themeName)
        {
            var app = Application.Current;
            if (app == null) return;

            try
            {
                // Clear existing theme dictionaries
                var themesToRemove = app.Resources.MergedDictionaries
                    .Where(d => d.Source != null && 
                           (d.Source.OriginalString.Contains("LightTheme") || 
                            d.Source.OriginalString.Contains("DarkTheme")))
                    .ToList();

                foreach (var theme in themesToRemove)
                {
                    app.Resources.MergedDictionaries.Remove(theme);
                }

                // Add new theme at the start (higher priority)
                var themeUri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);
                var newTheme = new ResourceDictionary { Source = themeUri };
                app.Resources.MergedDictionaries.Insert(0, newTheme);

                StatusMessage = $"Theme changed to {themeName}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
                MessageBox.Show($"Failed to apply theme: {ex.Message}\n\nMake sure theme files exist in Themes folder.", 
                    "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private RunpodService GetOrCreateService()
        {
            if (_runpodService == null)
            {
                _runpodService = new RunpodService(_apiKey, _endpointId);
            }
            return _runpodService;
        }

        private void ShowImage(BitmapImage? image)
        {
            if (image != null)
            {
                CurrentImage = image;
                IsGalleryView = false;
                
                // Calculate zoom to fit - will be applied in the UI
                // Set to a marker value that the UI will interpret as "fit to screen"
                ImageZoom = -1; // -1 indicates auto-fit
            }
        }

        private void LoadJobImages()
        {
            CurrentImages.Clear();
            IsGalleryView = true;
            CurrentImage = null;

            if (_selectedJob != null && _selectedJob.ImageFilePaths != null && _selectedJob.ImageFilePaths.Count > 0)
            {
                foreach (var filePath in _selectedJob.ImageFilePaths)
                {
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(filePath);
                            bitmap.EndInit();
                            CurrentImages.Add(bitmap);
                        }
                        catch
                        {
                            // Ignore if we can't load the image
                        }
                    }
                }
            }
        }

        private void UpdateSelectedJobStatus()
        {
            if (_selectedJob != null && !string.IsNullOrEmpty(_selectedJob.RawStatusResponse))
            {
                // Format JSON for better readability - keep raw data
                try
                {
                    var jsonDoc = JsonDocument.Parse(_selectedJob.RawStatusResponse);
                    SelectedJobStatusResponse = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    SelectedJobStatusResponse = _selectedJob.RawStatusResponse;
                }
                IsStatusResponseExpanded = false;
            }
            else
            {
                SelectedJobStatusResponse = "No status response available";
            }
        }

        private async Task RefreshJobStatusAsync()
        {
            if (_selectedJob == null || string.IsNullOrEmpty(_selectedJob.Id))
                return;

            try
            {
                var service = GetOrCreateService();
                var (response, rawJson) = await service.GetJobStatusAsync(_selectedJob.Id);

                if (response != null)
                {
                    _selectedJob.Status = response.Status?.ToLower() ?? "unknown";
                    _selectedJob.RawStatusResponse = rawJson;
                    
                    // Update worker ID if available
                    if (!string.IsNullOrEmpty(response.WorkerId))
                    {
                        _selectedJob.WorkerId = response.WorkerId;
                    }
                    
                    // Trigger UI update for the Jobs collection
                    OnPropertyChanged(nameof(Jobs));
                    
                    // Format and display
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(rawJson);
                        SelectedJobStatusResponse = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch
                    {
                        SelectedJobStatusResponse = rawJson;
                    }

                    StatusMessage = $"Refreshed status for job {_selectedJob.Id}";
                    SaveJobsToSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh job status:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveJob(RunpodJob? job)
        {
            if (job == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove job {job.Id}?\n\nThis will not delete the generated images from disk.",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (SelectedJob == job)
                {
                    SelectedJob = null;
                }
                Jobs.Remove(job);
                SaveJobsToSettings();
                StatusMessage = $"Removed job {job.Id}";
            }
        }

        private async Task RerunJobAsync(RunpodJob? job)
        {
            if (job == null || string.IsNullOrEmpty(job.WorkflowInputsJson))
            {
                MessageBox.Show("Cannot rerun this job: workflow inputs not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Restore the workflow inputs from the job
                LoadJobInputsInternal(job);

                StatusMessage = $"Rerunning job with saved inputs...";
                
                // Run the job with the restored inputs
                await RunJobAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rerun job:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CancelJobAsync(RunpodJob? job)
        {
            if (job == null || string.IsNullOrEmpty(job.Id))
            {
                MessageBox.Show("Cannot cancel this job: job ID not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to cancel job {job.Id}?",
                "Confirm Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusMessage = $"Cancelling job {job.Id}...";
                    var service = GetOrCreateService();
                    var success = await service.CancelJobAsync(job.Id);

                    if (success)
                    {
                        job.Status = "cancelled";
                        job.CompletedAt = DateTime.Now;
                        StatusMessage = $"Job {job.Id} cancelled successfully";

                        // Refresh the job status to get the latest state
                        await RefreshJobStatusAsync();

                        // Save jobs after cancellation
                        SaveJobsToSettings();
                    }
                    else
                    {
                        StatusMessage = $"Failed to cancel job {job.Id}";
                        MessageBox.Show($"Failed to cancel job {job.Id}. The job may have already completed or been cancelled.",
                            "Cancellation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error cancelling job: {ex.Message}";
                    MessageBox.Show($"Error cancelling job:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task CancelOrRemoveJobAsync(RunpodJob? job)
        {
            if (job == null) return;

            // Determine if this is a running job or finished job
            bool isRunning = job.Status == "pending" || job.Status == "in_queue" || job.Status == "in_progress";

            if (isRunning)
            {
                // Cancel the running job
                await CancelJobAsync(job);
            }
            else
            {
                // Remove the finished job
                RemoveJob(job);
            }
        }

        private void LoadJobInputs(RunpodJob? job)
        {
            if (job == null || string.IsNullOrEmpty(job.WorkflowInputsJson))
            {
                MessageBox.Show("Cannot load inputs: workflow inputs not available for this job.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LoadJobInputsInternal(job);
                StatusMessage = $"Loaded inputs from job {job.Id}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load job inputs:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadJobInputsInternal(RunpodJob job)
        {
            if (string.IsNullOrEmpty(job.WorkflowInputsJson))
            {
                return;
            }

            try
            {
                // Deserialize the saved workflow inputs
                var savedInputs = JsonSerializer.Deserialize<Dictionary<string, ObservableCollection<WorkflowInput>>>(
                    job.WorkflowInputsJson,
                    new JsonSerializerOptions 
                    { 
                        Converters = { new WorkflowInputConverter() },
                        PropertyNameCaseInsensitive = true
                    });

                if (savedInputs == null || savedInputs.Count == 0)
                {
                    return;
                }

                // If we don't have current workflow inputs, just use the saved ones
                if (WorkflowInputs.Count == 0)
                {
                    WorkflowInputs = savedInputs;
                    OnPropertyChanged(nameof(WorkflowInputs));
                    return;
                }

                // Merge the saved values into the current workflow inputs
                foreach (var category in savedInputs)
                {
                    if (!WorkflowInputs.ContainsKey(category.Key))
                        continue;

                    var currentInputs = WorkflowInputs[category.Key];
                    
                    foreach (var savedInput in category.Value)
                    {
                        if (savedInput == null) continue;

                        var currentInput = currentInputs.FirstOrDefault(i => 
                            i != null &&
                            i.NodeId == savedInput.NodeId && 
                            i.DisplayName == savedInput.DisplayName);

                        if (currentInput != null)
                        {
                            if (currentInput is TextInput currentText && savedInput is TextInput savedText)
                            {
                                currentText.Value = savedText.Value ?? "";
                            }
                            else if (currentInput is NumberInput currentNumber && savedInput is NumberInput savedNumber)
                            {
                                currentNumber.Value = savedNumber.Value ?? "";
                            }
                            else if (currentInput is NumberPairInput currentPair && savedInput is NumberPairInput savedPair)
                            {
                                currentPair.Value1 = savedPair.Value1;
                                currentPair.Value2 = savedPair.Value2;
                            }
                            else if (currentInput is LoraListInput currentLora && savedInput is LoraListInput savedLora)
                            {
                                currentLora.Loras.Clear();
                                if (savedLora.Loras != null)
                                {
                                    foreach (var lora in savedLora.Loras)
                                    {
                                        currentLora.Loras.Add(new LoraItem 
                                        { 
                                            Enabled = lora.Enabled,
                                            LoraName = lora.LoraName,
                                            Strength = lora.Strength
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                // Trigger UI update
                OnPropertyChanged(nameof(WorkflowInputs));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load job inputs:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveJobsToSettings()
        {
            try
            {
                // Keep only the most recent jobs
                var recentJobs = Jobs.Take(MaxRecentJobs).ToList();
                _settings.RecentJobs = recentJobs;
                _settings.Save();
            }
            catch
            {
                // Silently fail if we can't save jobs
            }
        }

        private void SaveSettings()
        {
            _settings.ApiKey = _apiKey;
            _settings.EndpointId = _endpointId;
            _settings.OutputFolder = _outputFolder;
            _settings.LastWorkflowPath = _workflowFilePath;
            _settings.SaveWorkflowWithJob = _saveWorkflowWithJob;
            _settings.Theme = _selectedTheme;
            _settings.MaxPollingAttempts = _maxPollingAttempts;
            _settings.Save();
        }

        private bool CanRunJob()
        {
            return !IsRunning && 
                   !string.IsNullOrEmpty(ApiKey) && 
                   !string.IsNullOrEmpty(EndpointId) &&
                   HasWorkflow;
        }

        private void LoadWorkflowFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select ComfyUI Workflow JSON File",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json"
            };

            // Start from the last workflow directory if available
            if (!string.IsNullOrEmpty(_workflowFilePath) && File.Exists(_workflowFilePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(_workflowFilePath);
            }

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    WorkflowFilePath = dialog.FileName;
                    var jsonContent = File.ReadAllText(dialog.FileName);
                    
                    // Validate it's valid JSON
                    JsonDocument.Parse(jsonContent);
                    
                    WorkflowJson = jsonContent;
                    StatusMessage = $"Loaded workflow from: {Path.GetFileName(dialog.FileName)}";
                }
                catch (JsonException ex)
                {
                    StatusMessage = "Invalid JSON file";
                    MessageBox.Show($"The selected file contains invalid JSON:\n{ex.Message}", 
                        "Invalid JSON", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Error loading workflow file";
                    MessageBox.Show($"Error loading workflow file:\n{ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ValidateWorkflow()
        {
            if (string.IsNullOrWhiteSpace(WorkflowJson))
            {
                MessageBox.Show("Workflow is empty. Please paste a workflow or load from file.", 
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var doc = JsonDocument.Parse(WorkflowJson);
                var formatted = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                
                MessageBox.Show("✓ Workflow JSON is valid!", 
                    "Validation Success", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusMessage = "Workflow validated successfully";
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"Invalid JSON:\n{ex.Message}", 
                    "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Workflow validation failed";
            }
        }

        private async Task RunJobAsync()
        {
            if (string.IsNullOrWhiteSpace(WorkflowJson))
            {
                StatusMessage = "No workflow loaded!";
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(EndpointId))
            {
                StatusMessage = "Please configure API key and Endpoint ID in Settings";
                return;
            }

            // Switch to Run tab (index 2: Workflow=0, Input=1, Run=2)
            SelectedTabIndex = 2;

            try
            {
                StatusMessage = "Submitting job...";

                // Validate image inputs: ensure IMG2IMG image inputs are not empty
                // Clear previous validation flags for image inputs
                foreach (var img in WorkflowInputs.Values.SelectMany(v => v).OfType<ImageInput>())
                {
                    img.HasValidationError = false;
                }
                
                var emptyImageInputs = WorkflowInputs.Values
                    .SelectMany(v => v)
                    .OfType<ImageInput>()
                    .Where(ii => string.IsNullOrWhiteSpace(ii.Base64Data))
                    .ToList();
                
                if (emptyImageInputs.Any())
                {
                    // Mark validation errors and inform user
                    foreach (var ii in emptyImageInputs)
                    {
                        ii.HasValidationError = true;
                    }

                    StatusMessage = "Please load images for all IMG2IMG inputs before running the job.";
                    MessageBox.Show("One or more IMG2IMG image inputs are empty. Please load an image or remove the input.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save the current input values for next time
                SaveLastInputValues();

                var service = GetOrCreateService();
                var modifiedWorkflow = WorkflowInputApplier.ApplyInputs(WorkflowJson, WorkflowInputs);
                var workflow = JsonSerializer.Deserialize<object>(modifiedWorkflow);

                var jobId = await service.SubmitJobAsync(workflow!);

                if (string.IsNullOrEmpty(jobId))
                {
                    StatusMessage = "Failed to submit job";
                    return;
                }

                // Serialize current workflow inputs to save with the job
                var workflowInputsJson = JsonSerializer.Serialize(
                    WorkflowInputs,
                    new JsonSerializerOptions 
                    { 
                        WriteIndented = false,
                        Converters = { new WorkflowInputConverter() },
                        PropertyNameCaseInsensitive = true
                    });

                var job = new RunpodJob
                {
                    Id = jobId,
                    Status = "pending",
                    CreatedAt = DateTime.Now,
                    WorkflowInputsJson = workflowInputsJson
                };
                Jobs.Insert(0, job);

                // Save MODIFIED workflow JSON file if setting is enabled
                if (SaveWorkflowWithJob && !string.IsNullOrEmpty(modifiedWorkflow))
                {
                    try
                    {
                        var workflowFileName = $"workflow_{jobId}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                        var workflowFilePath = Path.Combine(_outputFolder, workflowFileName);
                        
                        // Format the JSON nicely for readability
                        var formattedWorkflow = JsonSerializer.Serialize(
                            JsonSerializer.Deserialize<object>(modifiedWorkflow),
                            new JsonSerializerOptions { WriteIndented = true });
                        
                        File.WriteAllText(workflowFilePath, formattedWorkflow);
                        StatusMessage = $"Job {jobId} submitted. Workflow saved to {workflowFileName}. Polling...";
                    }
                    catch (Exception saveEx)
                    {
                        // Don't fail the job if workflow save fails, just log it
                        System.Diagnostics.Debug.WriteLine($"Failed to save workflow: {saveEx.Message}");
                    }
                }

                StatusMessage = $"Job {jobId} submitted. Polling...";

                var jobIdShort = jobId.Substring(0, 8);
                var progress = new Progress<string>(msg => 
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Update or add the status for this job
                        var statusLine = $"[Job {jobIdShort}...] {msg}";
                        
                        // Find existing status for this job
                        var existingIndex = -1;
                        for (int i = 0; i < ActiveJobStatuses.Count; i++)
                        {
                            if (ActiveJobStatuses[i].StartsWith($"[Job {jobIdShort}...]"))
                            {
                                existingIndex = i;
                                break;
                            }
                        }
                        
                        if (existingIndex >= 0)
                        {
                            ActiveJobStatuses[existingIndex] = statusLine;
                        }
                        else
                        {
                            ActiveJobStatuses.Add(statusLine);
                        }
                        
                        // Also update the main status message
                        StatusMessage = statusLine;
                    });
                });

                var completedJob = await service.PollJobUntilCompleteAsync(
                    job, 
                    maxAttempts: _maxPollingAttempts,
                    progress: progress,
                    onStatusUpdate: rawJson => 
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            job.RawStatusResponse = rawJson;
                            // Force UI update for the job status
                            OnPropertyChanged(nameof(Jobs));
                            if (SelectedJob?.Id == jobId)
                            {
                                UpdateSelectedJobStatus();
                            }
                        });
                    });

                if (completedJob != null)
                {
                    // Remove this job's status from active list
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = ActiveJobStatuses.Count - 1; i >= 0; i--)
                        {
                            if (ActiveJobStatuses[i].StartsWith($"[Job {jobIdShort}...]"))
                            {
                                ActiveJobStatuses.RemoveAt(i);
                            }
                        }
                    });
                    
                    // Force UI update for the job status in the ListView
                    Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(Jobs)));

                    if (completedJob.Status == "completed" && completedJob.AllImagesBase64 != null && completedJob.AllImagesBase64.Count > 0)
                    {
                        var wasInPreviewMode = !IsGalleryView;
                        
                        var filePaths = await SaveAndDisplayImagesAsync(completedJob.AllImagesBase64, jobId);
                        completedJob.ImageFilePaths = filePaths;
                        StatusMessage = $"Job {jobId} completed successfully! Generated {filePaths.Count} image(s).";
                        
                        // If we were in preview mode, automatically switch to the completed job and show first image
                        if (wasInPreviewMode && filePaths.Count > 0)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Temporarily disable auto-gallery mode for this job selection
                                SelectedJob = completedJob;
                                // LoadJobImages is called by SelectedJob setter and loads images into CurrentImages
                                // Now explicitly show the first image
                                System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (CurrentImages.Count > 0)
                                        {
                                            ShowImage(CurrentImages[0]);
                                        }
                                    });
                                });
                            });
                        }
                        
                        // Show Windows notification
                        ShowNotification();
                    }
                    else
                    {
                        StatusMessage = $"Job {jobId} failed: {completedJob.ErrorMessage}";
                        ShowNotification();
                    }

                    if (SelectedJob?.Id == jobId)
                    {
                        UpdateSelectedJobStatus();
                        LoadJobImages();
                    }

                    // Save jobs after completion
                    SaveJobsToSettings();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task<List<string>> SaveAndDisplayImagesAsync(List<string> base64Images, string? jobId)
        {
            return await Task.Run(() =>
            {
                var filePaths = new List<string>();
                int imageIndex = 0;

                foreach (var base64Image in base64Images)
                {
                    try
                    {
                        // Remove data URI prefix if present
                        var base64Data = base64Image.Contains(",") ? base64Image.Split(',')[1] : base64Image;
                        var imageBytes = Convert.FromBase64String(base64Data);

                        // Save to disk
                        var fileName = base64Images.Count > 1 
                            ? $"output_{jobId}_{imageIndex}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                            : $"output_{jobId}_{DateTime.Now:yyyyMMdd_HHmms}.png";
                        var filePath = Path.Combine(_outputFolder, fileName);
                        File.WriteAllBytes(filePath, imageBytes);
                        filePaths.Add(filePath);

                        imageIndex++;
                    }
                    catch
                    {
                        // Skip images that fail to save
                    }
                }

                // Display images in gallery
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentImages.Clear();
                    foreach (var filePath in filePaths)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(filePath);
                        bitmap.EndInit();
                        CurrentImages.Add(bitmap);
                    }
                    IsGalleryView = true;
                });

                return filePaths;
            });
        }

        private void ShowNotification()
        {
            try
            {
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
                FlashWindow(windowHandle, true);
            }
            catch
            {
                // Silently fail if notifications aren't supported
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        private void OpenImageFolder()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _outputFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select Output Folder",
                FileName = "folder",
                Filter = "Folder|*.folder"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputFolder = Path.GetDirectoryName(dialog.FileName) ?? _outputFolder;
            }
        }

        private void SaveImageAs()
        {
            if (CurrentImage == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Image As",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
                DefaultExt = ".png",
                FileName = $"image_{DateTime.Now:yyyyMMdd_H Hmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Find the source file path from CurrentImage
                    var sourceUri = CurrentImage.UriSource;
                    if (sourceUri != null && File.Exists(sourceUri.LocalPath))
                    {
                        File.Copy(sourceUri.LocalPath, dialog.FileName, true);
                        StatusMessage = $"Image saved to: {dialog.FileName}";
                    }
                    else
                    {
                        // Fallback: encode the BitmapImage to the selected format
                        BitmapEncoder encoder = dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            ? new JpegBitmapEncoder()
                            : new PngBitmapEncoder();

                        encoder.Frames.Add(BitmapFrame.Create(CurrentImage));
                        using (var fileStream = new FileStream(dialog.FileName, FileMode.Create))
                        {
                            encoder.Save(fileStream);
                        }
                        StatusMessage = $"Image saved to: {dialog.FileName}";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save image:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ParseWorkflowInputs()
        {
            if (string.IsNullOrWhiteSpace(WorkflowJson))
            {
                WorkflowInputs = new();
                Categories.Clear();
                CategoryViewModels.Clear();
                return;
            }

            WorkflowInputs = WorkflowInputParser.ParseWorkflow(WorkflowJson);
            
            // Only restore last input values on initial app load, not when manually loading a workflow
            if (_isInitialLoad)
            {
                RestoreLastInputValues();
                _isInitialLoad = false; // After first load, don't auto-restore anymore
            }
            
            // Update categories and build view models
            UpdateCategoriesFromInputs();
            BuildCategoryViewModels();
            
            // Initialize named variable hints for Character, Costume, and Character Pose categories
            UpdateNamedVariableHints("Character");
            UpdateNamedVariableHints("Costume");
            UpdateNamedVariableHints("Character Pose");
        }

        private void RestoreLastInputValues()
        {
            if (string.IsNullOrEmpty(_settings.LastInputValuesJson))
                return;

            try
            {
                var savedInputs = JsonSerializer.Deserialize<Dictionary<string, ObservableCollection<WorkflowInput>>>(
                    _settings.LastInputValuesJson,
                    new JsonSerializerOptions 
                    { 
                        Converters = { new WorkflowInputConverter() },
                        PropertyNameCaseInsensitive = true
                    });

                if (savedInputs == null || savedInputs.Count == 0)
                    return;

                // Merge the saved values into the current workflow inputs
                foreach (var category in savedInputs)
                {
                    if (!WorkflowInputs.ContainsKey(category.Key))
                        continue;

                    var currentInputs = WorkflowInputs[category.Key];
                    
                    foreach (var savedInput in category.Value)
                    {
                        if (savedInput == null) continue;

                        var matchingInput = currentInputs.FirstOrDefault(i => 
                            i.NodeId == savedInput.NodeId && i.DisplayName == savedInput.DisplayName);

                        if (matchingInput == null) continue;

                        // Restore values based on input type
                        if (matchingInput is TextInput currentText && savedInput is TextInput savedText)
                        {
                            currentText.Value = savedText.Value;
                        }
                        else if (matchingInput is NumberInput currentNumber && savedInput is NumberInput savedNumber)
                        {
                            currentNumber.Value = savedNumber.Value;
                        }
                        else if (matchingInput is NumberPairInput currentPair && savedInput is NumberPairInput savedPair)
                        {
                            currentPair.Value1 = savedPair.Value1;
                            currentPair.Value2 = savedPair.Value2;
                        }
                        else if (matchingInput is LoraListInput currentLora && savedInput is LoraListInput savedLora)
                        {
                            currentLora.Loras.Clear();
                            foreach (var lora in savedLora.Loras)
                            {
                                currentLora.Loras.Add(new LoraItem
                                {
                                    Enabled = lora.Enabled,
                                    LoraName = lora.LoraName,
                                    Strength = lora.Strength
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if we can't restore last inputs
            }
        }

        private void SaveLastInputValues()
        {
            try
            {
                var inputsJson = JsonSerializer.Serialize(
                    WorkflowInputs,
                    new JsonSerializerOptions 
                    { 
                        WriteIndented = false,
                        Converters = { new WorkflowInputConverter() },
                        PropertyNameCaseInsensitive = true
                    });

                _settings.LastInputValuesJson = inputsJson;
                _settings.Save();
            }
            catch
            {
                // Silently fail if we can't save inputs
            }
        }

        private void BuildCategoryViewModels()
        {
            CategoryViewModels.Clear();
            PrimaryViewModels.Clear();
            SecondaryViewModels.Clear();

            foreach (var category in Categories)
            {
                if (WorkflowInputs.TryGetValue(category.Name, out var inputs))
                {
                    var viewModel = new CategoryViewModel
                    {
                        CategoryInfo = category,
                        Inputs = inputs
                    };
                    CategoryViewModels.Add(viewModel);

                    // Add to appropriate view
                    if (category.ViewIndex == 0)
                    {
                        PrimaryViewModels.Add(viewModel);
                    }
                    else
                    {
                        SecondaryViewModels.Add(viewModel);
                    }
                }
            }

            OnPropertyChanged(nameof(HasSecondaryCategories));
        }

        private void UpdateCategoriesFromInputs()
        {
            // Get existing preferences
            var preferences = _settings.CategoryPreferences ?? new Dictionary<string, CategoryPreference>();

            // Create category info for each category in WorkflowInputs
            var newCategories = new List<CategoryInfo>();
            int defaultOrder = 0;

            foreach (var categoryKey in WorkflowInputs.Keys)
            {
                var categoryInfo = new CategoryInfo
                {
                    Name = categoryKey,
                    Order = preferences.ContainsKey(categoryKey) ? preferences[categoryKey].Order : defaultOrder++,
                    IsCollapsed = preferences.ContainsKey(categoryKey) && preferences[categoryKey].IsCollapsed,
                    ViewIndex = preferences.ContainsKey(categoryKey) ? preferences[categoryKey].ViewIndex : 0
                };

                newCategories.Add(categoryInfo);
            }

            // Sort by order
            newCategories = newCategories.OrderBy(c => c.Order).ToList();

            // Update the observable collection
            Categories.Clear();
            foreach (var cat in newCategories)
            {
                Categories.Add(cat);
            }
        }
        private bool CanMoveCategoryUp(CategoryInfo? category)
        {
            if (category == null) return false;
            var index = Categories.IndexOf(category);
            return index > 0;
        }

        private bool CanMoveCategoryDown(CategoryInfo? category)
        {
            if (category == null) return false;
            var index = Categories.IndexOf(category);
            return index >= 0 && index < Categories.Count - 1;
        }

        private void MoveCategoryUp(CategoryInfo? category)
        {
            if (!CanMoveCategoryUp(category) || category == null) return;
    
            var index = Categories.IndexOf(category);
            Categories.Move(index, index - 1);
    
            // Update order values
            for (int i = 0; i < Categories.Count; i++)
            {
                Categories[i].Order = i;
            }
    
            // Rebuild view models with new order
            BuildCategoryViewModels();
            SaveCategoryPreferencesDebounced(); // Changed from SaveCategoryPreferences()
        }

        private void MoveCategoryDown(CategoryInfo? category)
        {
            if (!CanMoveCategoryDown(category) || category == null) return;
    
            var index = Categories.IndexOf(category);
            Categories.Move(index, index + 1);
    
            // Update order values
            for (int i = 0; i < Categories.Count; i++)
            {
                Categories[i].Order = i;
            }
    
            // Rebuild view models with new order
            BuildCategoryViewModels();
            SaveCategoryPreferencesDebounced(); // Changed from SaveCategoryPreferences()
        }

        private void ToggleCategory(CategoryInfo? category)
        {
            if (category == null) return;
    
            category.IsCollapsed = !category.IsCollapsed;
            SaveCategoryPreferencesDebounced();
        }

        private void SaveCategoryPreferences()
        {
            var preferences = new Dictionary<string, CategoryPreference>();

            foreach (var category in Categories)
            {
                preferences[category.Name] = new CategoryPreference
                {
                    Order = category.Order,
                    IsCollapsed = category.IsCollapsed,
                    ViewIndex = category.ViewIndex
                };
            }

            _settings.CategoryPreferences = preferences;
            _settings.Save();
        }
        private void SaveCategoryPreferencesDebounced()
        {
            lock (_saveLock)
            {
                // Cancel any pending save
                _saveCategoryCts?.Cancel();
                _saveCategoryCts = new CancellationTokenSource();
                
                var cts = _saveCategoryCts;

                // Save after 500ms of inactivity (debounce)
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(500, cts.Token);
                        
                        if (!cts.Token.IsCancellationRequested)
                        {
                            SaveCategoryPreferences();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected when user toggles rapidly
                    }
                });
            }
        }

        // NEW: Navigation methods
        private bool CanNavigateToPreviousJob()
        {
            if (Jobs.Count == 0 || SelectedJob == null) return false;
            
            // Check if there's at least one other completed job
            return Jobs.Any(j => j.Status == "completed" && j != SelectedJob);
        }

        private bool CanNavigateToNextJob()
        {
            if (Jobs.Count == 0 || SelectedJob == null) return false;
            
            // Check if there's at least one other completed job
            return Jobs.Any(j => j.Status == "completed" && j != SelectedJob);
        }

        private void NavigateToPreviousJob()
        {
            if (!CanNavigateToPreviousJob()) return;
            
            var currentIndex = Jobs.IndexOf(SelectedJob!);
            var wasInPreviewMode = !IsGalleryView;
            
            // Find the previous completed job with images (going up in the list)
            int previousIndex = currentIndex - 1;
            if (previousIndex < 0) previousIndex = Jobs.Count - 1; // Start from bottom
            
            // Search for the previous completed job with images, wrapping around if needed
            int searchCount = 0;
            while (searchCount < Jobs.Count)
            {
                var job = Jobs[previousIndex];
                if (job.Status == "completed" && 
                    job.ImageFilePaths != null && 
                    job.ImageFilePaths.Count > 0)
                {
                    SelectedJob = job;
                    
                    // If we were in preview mode, open the first image of the new job
                    if (wasInPreviewMode && CurrentImages.Count > 0)
                    {
                        ShowImage(CurrentImages[0]);
                        // Notify to trigger re-evaluation
                        OnPropertyChanged(nameof(ImageZoom));
                    }
                    return;
                }
                
                previousIndex--;
                if (previousIndex < 0) previousIndex = Jobs.Count - 1;
                searchCount++;
            }
        }

        private void NavigateToNextJob()
        {
            if (!CanNavigateToNextJob()) return;
            
            var currentIndex = Jobs.IndexOf(SelectedJob!);
            var wasInPreviewMode = !IsGalleryView;
            
            // Find the next completed job with images (going down in the list)
            int nextIndex = currentIndex + 1;
            if (nextIndex >= Jobs.Count) nextIndex = 0; // Start from top
            
            // Search for the next completed job with images, wrapping around if needed
            int searchCount = 0;
            while (searchCount < Jobs.Count)
            {
                var job = Jobs[nextIndex];
                if (job.Status == "completed" && 
                    job.ImageFilePaths != null && 
                    job.ImageFilePaths.Count > 0)
                {
                    SelectedJob = job;
                    
                    // If we were in preview mode, open the first image of the new job
                    if (wasInPreviewMode && CurrentImages.Count > 0)
                    {
                        ShowImage(CurrentImages[0]);
                        // Notify to trigger re-evaluation
                        OnPropertyChanged(nameof(ImageZoom));
                    }
                    return;
                }
                
                nextIndex++;
                if (nextIndex >= Jobs.Count) nextIndex = 0;
                searchCount++;
            }
        }

        private void AddLora(object? parameter)
        {
            var loraListInput = parameter as LoraListInput;
            if (loraListInput == null) return;
    
            loraListInput.Loras.Add(new LoraItem
            {
                Enabled = true,
                LoraName = "",
                Strength = 1.0
            });
        }

        private void RemoveLora(LoraItem? loraItem)
        {
            if (loraItem == null) return;
    
            // Find the LoraListInput that contains this LoraItem
            foreach (var category in WorkflowInputs.Values)
            {
                foreach (var input in category)
                {
                    if (input is LoraListInput loraListInput && loraListInput.Loras.Contains(loraItem))
                    {
                        loraListInput.Loras.Remove(loraItem);
                        return;
                    }
                }
            }
        }

        private void CopyToClipboard(string? text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                Clipboard.SetText(text);
                StatusMessage = $"Copied to clipboard: {text}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy to clipboard: {ex.Message}";
            }
        }

        private void UpdateNamedVariableHints(string? category)
        {
            if (string.IsNullOrEmpty(category)) return;
            if (!WorkflowInputs.ContainsKey(category)) return;

            // Only update for Character, Costume, and Character Pose categories
            if (category != "Character" && category != "Costume" && category != "Character Pose") return;

            var inputs = WorkflowInputs[category];
            
            // Find the Name field
            var nameInput = inputs.FirstOrDefault(i => i.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase));
            if (nameInput is not TextInput textNameInput) return;

            var nameValue = textNameInput.Value?.Trim();
            
            if (string.IsNullOrEmpty(nameValue)) 
            {
                // Clear all hints in this category (empty name is invalid)
                foreach (var input in inputs)
                {
                    input.VariableHint = "";
                    input.NamedVariableHint = "";
                }
                ValidateUniqueNames();
                return;
            }

            // Validate that name only contains letters, numbers, and underscores
            bool isValidName = nameValue.All(c => char.IsLetterOrDigit(c) || c == '_');
            if (!isValidName)
            {
                // Clear all hints in this category if name is invalid
                foreach (var input in inputs)
                {
                    input.VariableHint = "";
                    input.NamedVariableHint = "";
                }
                ValidateUniqueNames();
                return;
            }

            // Generate prefix from category
            var prefix = category switch
            {
                "Character" => "Character",
                "Costume" => "Costume",
                "Character Pose" => "CharaPose",
                _ => null
            };

            if (prefix == null) return;

            // Update all inputs in this category except Name
            foreach (var input in inputs)
            {
                if (input.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    input.NamedVariableHint = "";
                    continue;
                }

                // Generate variable suffix from DisplayName
                var variableSuffix = new StringBuilder();
                foreach (var c in input.DisplayName)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        variableSuffix.Append(c);
                    }
                }

                if (variableSuffix.Length > 0)
                {
                    input.NamedVariableHint = $"{{{nameValue}_{variableSuffix}}}";
                }
                else
                {
                    input.NamedVariableHint = "";
                }
            }
            
            // Validate unique names after updating hints
            ValidateUniqueNames();
        }

        private void ValidateUniqueNames()
        {
            // Only validate for Character, Costume, and Character Pose categories
            var categoriesToValidate = new[] { "Character", "Costume", "Character Pose" };
            
            // Collect all Name fields with valid values
            var nameFields = new List<(string Category, TextInput Input, string Value)>();
            
            // Track categories with invalid names (including empty names)
            var categoriesWithInvalidNames = new HashSet<string>();
            
            foreach (var category in categoriesToValidate)
            {
                if (!WorkflowInputs.ContainsKey(category)) continue;
                
                var nameInput = WorkflowInputs[category]
                    .FirstOrDefault(i => i.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase));
                
                if (nameInput is TextInput textInput)
                {
                    var value = textInput.Value ?? "";
                    
                    // Empty name is invalid
                    if (string.IsNullOrEmpty(value))
                    {
                        categoriesWithInvalidNames.Add(category);
                    }
                    else
                    {
                        // Check if name contains ONLY letters, numbers, and underscores
                        bool isValid = value.All(c => char.IsLetterOrDigit(c) || c == '_');
                        
                        if (isValid)
                        {
                            nameFields.Add((category, textInput, value));
                        }
                        else
                        {
                            categoriesWithInvalidNames.Add(category);
                        }
                    }
                }
            }
            
            // Clear all validation errors first
            foreach (var category in categoriesToValidate)
            {
                if (!WorkflowInputs.ContainsKey(category)) continue;
                
                var nameInput = WorkflowInputs[category]
                    .FirstOrDefault(i => i.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase));
                
                if (nameInput != null)
                {
                    nameInput.HasValidationError = false;
                }
            }
            
            // Find duplicates and mark them
            var categoriesWithDuplicates = new HashSet<string>();
            
            for (int i = 0; i < nameFields.Count; i++)
            {
                for (int j = i + 1; j < nameFields.Count; j++)
                {
                    if (nameFields[i].Value.Equals(nameFields[j].Value, StringComparison.OrdinalIgnoreCase))
                    {
                        nameFields[i].Input.HasValidationError = true;
                        nameFields[j].Input.HasValidationError = true;
                        categoriesWithDuplicates.Add(nameFields[i].Category);
                        categoriesWithDuplicates.Add(nameFields[j].Category);
                    }
                }
            }
            
            // Mark invalid names with red border
            foreach (var category in categoriesWithInvalidNames)
            {
                if (!WorkflowInputs.ContainsKey(category)) continue;
                
                var nameInput = WorkflowInputs[category]
                    .FirstOrDefault(i => i.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase));
                
                if (nameInput != null)
                {
                    nameInput.HasValidationError = true;
                }
            }
            
            // Combine all problematic categories
            var categoriesToHideHints = new HashSet<string>(categoriesWithDuplicates);
            categoriesToHideHints.UnionWith(categoriesWithInvalidNames);
            
            // Hide ALL hints in problematic categories
            foreach (var category in categoriesToHideHints)
            {
                if (!WorkflowInputs.ContainsKey(category)) continue;
                
                var inputs = WorkflowInputs[category];
                foreach (var input in inputs)
                {
                    input.VariableHint = "";
                    input.NamedVariableHint = "";
                }
            }
            
            // Restore VariableHint for valid categories
            foreach (var category in categoriesToValidate)
            {
                if (categoriesToHideHints.Contains(category)) continue;
                if (!WorkflowInputs.ContainsKey(category)) continue;
                
                var inputs = WorkflowInputs[category];
                
                var prefix = category switch
                {
                    "Character" => "Character",
                    "Costume" => "Costume",
                    "Character Pose" => "CharaPose",
                    _ => null
                };
                
                if (prefix == null) continue;
                
                foreach (var input in inputs)
                {
                    if (input.DisplayName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        input.VariableHint = "";
                        continue;
                    }
                    
                    var variableSuffix = new StringBuilder();
                    foreach (var c in input.DisplayName)
                    {
                        if (char.IsLetterOrDigit(c))
                        {
                            variableSuffix.Append(c);
                        }
                    }
                    
                    if (variableSuffix.Length > 0)
                    {
                        input.VariableHint = $"{{{prefix}_{variableSuffix}}}";
                    }
                    else
                    {
                        input.VariableHint = "";
                    }
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
