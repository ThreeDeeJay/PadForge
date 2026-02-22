using CommunityToolkit.Mvvm.ComponentModel;

namespace PadForge.ViewModels
{
    /// <summary>
    /// Base class for all PadForge view models.
    /// Inherits <see cref="ObservableObject"/> from CommunityToolkit.Mvvm,
    /// which provides INotifyPropertyChanged and SetProperty helpers.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        private string _title = string.Empty;

        /// <summary>
        /// Display title for the view. Used by navigation and page headers.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
