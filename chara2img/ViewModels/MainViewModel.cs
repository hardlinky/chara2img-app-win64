using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<RunpodJob> Jobs { get; } = new();
        public ObservableCollection<BitmapImage> CurrentImages { get; } = new();

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

        public string WorkflowJson
        {
            get => _workflowJson;
            set
            {
                _workflowJson = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWorkflow));
                ParseWorkflowInputs(); // Add this line
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
                if (_isStatusResponseExpanded || string.IsNullOrEmpty(_selectedJobStatusResponse))
                    return _selectedJobStatusResponse;

                // Show only first 500 characters when collapsed
                return _selectedJobStatusResponse.Length > 500 
                    ? _selectedJobStatusResponse.Substring(0, 500) + "...\n\n[Click 'Show Full Response' to see more]"
                    : _selectedJobStatusResponse;
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
        public ICommand RemoveJobCommand { get; }
        public ICommand RerunJobCommand { get; }
        public ICommand LoadJobInputsCommand { get; }

        // Add new command for navigating jobs
        public ICommand NavigateToPreviousJobCommand { get; }
        public ICommand NavigateToNextJobCommand { get; }

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
            RemoveJobCommand = new RelayCommand<RunpodJob>(RemoveJob, job => job != null);
            RerunJobCommand = new RelayCommand<RunpodJob>(async job => await RerunJobAsync(job), job => job != null && !string.IsNullOrEmpty(job?.WorkflowInputsJson) && !IsRunning);
            LoadJobInputsCommand = new RelayCommand<RunpodJob>(LoadJobInputs, job => job != null && !string.IsNullOrEmpty(job?.WorkflowInputsJson));
            NavigateToPreviousJobCommand = new RelayCommand(NavigateToPreviousJob, CanNavigateToPreviousJob);
            NavigateToNextJobCommand = new RelayCommand(NavigateToNextJob, CanNavigateToNextJob);

            // Load settings
            _settings = AppSettings.Load();
            _apiKey = _settings.ApiKey;
            _endpointId = _settings.EndpointId;
            
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

            // Notify all properties to update UI with loaded values
            OnPropertyChanged(nameof(ApiKey));
            OnPropertyChanged(nameof(EndpointId));
            OnPropertyChanged(nameof(OutputFolder));
            OnPropertyChanged(nameof(WorkflowFilePath));
            OnPropertyChanged(nameof(WorkflowJson));
            OnPropertyChanged(nameof(HasWorkflow));
            OnPropertyChanged(nameof(WorkflowInputs)); // Add this to ensure inputs are updated
            OnPropertyChanged(nameof(SaveWorkflowWithJob));
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
                // Format JSON for better readability
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

                var progress = new Progress<string>(msg => StatusMessage = msg);
                var completedJob = await service.PollJobUntilCompleteAsync(
                    job, 
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
                    // Force UI update for the job status in the ListView
                    Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(Jobs)));

                    if (completedJob.Status == "completed" && completedJob.AllImagesBase64 != null && completedJob.AllImagesBase64.Count > 0)
                    {
                        var filePaths = await SaveAndDisplayImagesAsync(completedJob.AllImagesBase64, jobId);
                        completedJob.ImageFilePaths = filePaths;
                        StatusMessage = $"Job {jobId} completed successfully! Generated {filePaths.Count} image(s).";
                        
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
                FileName = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png"
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
                return;
            }

            WorkflowInputs = WorkflowInputParser.ParseWorkflow(WorkflowJson);
        }

        // NEW: Navigation methods
        private bool CanNavigateToPreviousJob()
        {
            // Can always navigate if there are jobs (will loop)
            return Jobs.Count > 0 && SelectedJob != null;
        }

        private bool CanNavigateToNextJob()
        {
            // Can always navigate if there are jobs (will loop)
            return Jobs.Count > 0 && SelectedJob != null;
        }

        private void NavigateToPreviousJob()
        {
            if (!CanNavigateToPreviousJob()) return;
            
            var currentIndex = Jobs.IndexOf(SelectedJob!);
            // Loop to last job if at first (top of list)
            var previousIndex = currentIndex == 0 ? Jobs.Count - 1 : currentIndex - 1;
            var previousJob = Jobs[previousIndex];
            var wasInPreviewMode = !IsGalleryView;
            
            SelectedJob = previousJob;
            
            // If we were in preview mode, open the first image of the new job
            if (wasInPreviewMode && CurrentImages.Count > 0)
            {
                ShowImage(CurrentImages[0]);
                // Notify to trigger re-evaluation
                OnPropertyChanged(nameof(ImageZoom));
            }
        }

        private void NavigateToNextJob()
        {
            if (!CanNavigateToNextJob()) return;
            
            var currentIndex = Jobs.IndexOf(SelectedJob!);
            // Loop to first job if at last (bottom of list)
            var nextIndex = currentIndex == Jobs.Count - 1 ? 0 : currentIndex + 1;
            var nextJob = Jobs[nextIndex];
            var wasInPreviewMode = !IsGalleryView;
            
            SelectedJob = nextJob;
            
            // If we were in preview mode, open the first image of the new job
            if (wasInPreviewMode && CurrentImages.Count > 0)
            {
                ShowImage(CurrentImages[0]);
                // Notify to trigger re-evaluation
                OnPropertyChanged(nameof(ImageZoom));
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
