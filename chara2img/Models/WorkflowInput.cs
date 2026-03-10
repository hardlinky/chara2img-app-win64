using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace chara2img.Models
{
    public class WorkflowInput : INotifyPropertyChanged
    {
        private string _nodeId = "";
        private string _nodeTitle = "";
        private string _category = "";
        private string _displayName = "";
        private string _inputType = "";
        private string _variableHint = "";
        private string _namedVariableHint = "";
        private bool _hasValidationError;

        public string NodeId
        {
            get => _nodeId;
            set { _nodeId = value; OnPropertyChanged(); }
        }

        public string NodeTitle
        {
            get => _nodeTitle;
            set { _nodeTitle = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public string InputType
        {
            get => _inputType;
            set { _inputType = value; OnPropertyChanged(); }
        }

        public string VariableHint
        {
            get => _variableHint;
            set { _variableHint = value; OnPropertyChanged(); }
        }

        public string NamedVariableHint
        {
            get => _namedVariableHint;
            set { _namedVariableHint = value; OnPropertyChanged(); }
        }

        public bool HasValidationError
        {
            get => _hasValidationError;
            set { _hasValidationError = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TextInput : WorkflowInput
    {
        private string _value = "";
        private string _inputKey = "";

        public string InputKey
        {
            get => _inputKey;
            set { _inputKey = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }
    }

    public class NumberInput : WorkflowInput
    {
        private string _value = "";
        private string _inputKey = "";
        private bool _isInteger;

        public string InputKey
        {
            get => _inputKey;
            set { _inputKey = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public bool IsInteger
        {
            get => _isInteger;
            set { _isInteger = value; OnPropertyChanged(); }
        }
    }

    public class NumberPairInput : WorkflowInput
    {
        private int _value1;
        private int _value2;
        private string _inputKey1 = "";
        private string _inputKey2 = "";
        private string _label1 = "";
        private string _label2 = "";

        public string InputKey1
        {
            get => _inputKey1;
            set { _inputKey1 = value; OnPropertyChanged(); }
        }

        public string InputKey2
        {
            get => _inputKey2;
            set { _inputKey2 = value; OnPropertyChanged(); }
        }

        public string Label1
        {
            get => _label1;
            set { _label1 = value; OnPropertyChanged(); }
        }

        public string Label2
        {
            get => _label2;
            set { _label2 = value; OnPropertyChanged(); }
        }

        public int Value1
        {
            get => _value1;
            set { _value1 = value; OnPropertyChanged(); }
        }

        public int Value2
        {
            get => _value2;
            set { _value2 = value; OnPropertyChanged(); }
        }
    }

    public class LoraItem : INotifyPropertyChanged
    {
        private bool _enabled;
        private string _loraName = "";
        private double _strength = 1.0;

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        public string LoraName
        {
            get => _loraName;
            set { _loraName = value; OnPropertyChanged(); }
        }

        public double Strength
        {
            get => _strength;
            set { _strength = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LoraListInput : WorkflowInput
    {
        private System.Collections.ObjectModel.ObservableCollection<LoraItem> _loras = new();

        public System.Collections.ObjectModel.ObservableCollection<LoraItem> Loras
        {
            get => _loras;
            set { _loras = value; OnPropertyChanged(); }
        }
    }

    public class BooleanInput : WorkflowInput
    {
        private bool _value;
        private string _inputKey = "";

        public string InputKey
        {
            get => _inputKey;
            set { _inputKey = value; OnPropertyChanged(); }
        }

        public bool Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }
    }
}