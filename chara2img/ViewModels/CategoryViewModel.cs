using System.Collections.ObjectModel;
using chara2img.Models;

namespace chara2img.ViewModels
{
    public class CategoryViewModel
    {
        public CategoryInfo CategoryInfo { get; set; } = new();
        public ObservableCollection<WorkflowInput> Inputs { get; set; } = new();
    }
}