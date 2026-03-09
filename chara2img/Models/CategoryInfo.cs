using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace chara2img.Models
{
    public class CategoryInfo : INotifyPropertyChanged
    {
        private string _name = "";
        private bool _isCollapsed = false;
        private int _order = 0;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set 
            { 
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}