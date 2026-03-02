using System;
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
        private RunpodService? _runPodService;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<RunpodJob> Jobs { get; } = new();

        public string ApiKey
        {
            get => _apiKey;
            set 
            { 
                _apiKey = value; 
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public string EndpointId
        {
            get => _endpointId;
            set 
            { 
                _endpointId = value; 
                OnPropertyChanged();
                SaveSettings();
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
            set { _workflowJson = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWorkflow)); }
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

        public ICommand RunJobCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand LoadWorkflowCommand { get; }
        public ICommand ValidateWorkflowCommand { get; }
        public ICommand RefreshJobStatusCommand { get; }
        public ICommand ToggleStatusResponseCommand { get; }

        public MainViewModel()
        {
            RunJobCommand = new RelayCommand(async () => await RunJobAsync(), CanRunJob);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            LoadWorkflowCommand = new RelayCommand(LoadWorkflowFile);
            ValidateWorkflowCommand = new RelayCommand(ValidateWorkflow);
            RefreshJobStatusCommand = new RelayCommand(async () => await RefreshJobStatusAsync(), () => _selectedJob != null && !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_endpointId));
            ToggleStatusResponseCommand = new RelayCommand(() => IsStatusResponseExpanded = !IsStatusResponseExpanded);

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
                }
                catch
                {
                    // Ignore if we can't load the last workflow
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
                _runPodService = new RunpodService(_apiKey, _endpointId);
                var (response, rawJson) = await _runPodService.GetJobStatusAsync(_selectedJob.Id);

                if (response != null)
                {
                    _selectedJob.Status = response.Status?.ToLower() ?? "unknown";
                    _selectedJob.RawStatusResponse = rawJson;
                    
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh job status:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettings()
        {
            _settings.ApiKey = _apiKey;
            _settings.EndpointId = _endpointId;
            _settings.OutputFolder = _outputFolder;
            _settings.LastWorkflowPath = _workflowFilePath;
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
            try
            {
                IsRunning = true;
                StatusMessage = "Initializing...";

                _runPodService = new RunpodService(_apiKey, _endpointId);

                // Parse the workflow JSON
                object? workflowInput;
                try
                {
                    workflowInput = JsonSerializer.Deserialize<object>(WorkflowJson);
                }
                catch (JsonException ex)
                {
                    StatusMessage = "Invalid workflow JSON";
                    MessageBox.Show($"Invalid workflow JSON:\n{ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (workflowInput == null)
                {
                    StatusMessage = "Workflow JSON is empty or invalid";
                    MessageBox.Show("Workflow JSON is empty or invalid.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusMessage = "Submitting job...";
                var jobId = await _runPodService.SubmitJobAsync(workflowInput);

                if (string.IsNullOrEmpty(jobId))
                {
                    StatusMessage = "Failed to submit job";
                    return;
                }

                var job = new RunpodJob
                {
                    Id = jobId,
                    Status = "pending",
                    CreatedAt = DateTime.Now
                };
                Jobs.Insert(0, job);

                StatusMessage = $"Job {jobId} submitted. Polling...";

                var progress = new Progress<string>(msg => StatusMessage = msg);
                var completedJob = await _runPodService.PollJobUntilCompleteAsync(
                    jobId, 
                    progress: progress,
                    onStatusUpdate: rawJson => 
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            job.RawStatusResponse = rawJson;
                            if (SelectedJob?.Id == jobId)
                            {
                                UpdateSelectedJobStatus();
                            }
                        });
                    });

                if (completedJob != null)
                {
                    job.Status = completedJob.Status;
                    job.CompletedAt = completedJob.CompletedAt;
                    job.ErrorMessage = completedJob.ErrorMessage;
                    job.ImageBase64 = completedJob.ImageBase64;
                    job.RawStatusResponse = completedJob.RawStatusResponse;

                    if (completedJob.Status == "completed" && !string.IsNullOrEmpty(completedJob.ImageBase64))
                    {
                        await SaveAndDisplayImageAsync(completedJob.ImageBase64, jobId);
                        StatusMessage = $"Job {jobId} completed successfully!";
                    }
                    else
                    {
                        StatusMessage = $"Job {jobId} failed: {completedJob.ErrorMessage}";
                    }

                    if (SelectedJob?.Id == jobId)
                    {
                        UpdateSelectedJobStatus();
                    }
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

        private async Task SaveAndDisplayImageAsync(string base64Image, string? jobId)
        {
            await Task.Run(() =>
            {
                // Remove data URI prefix if present
                var base64Data = base64Image.Contains(",") ? base64Image.Split(',')[1] : base64Image;
                var imageBytes = Convert.FromBase64String(base64Data);

                // Save to disk
                var fileName = $"output_{jobId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(_outputFolder, fileName);
                File.WriteAllBytes(filePath, imageBytes);

                // Display image
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.EndInit();
                    CurrentImage = bitmap;
                });
            });
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
}
