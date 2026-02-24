using System.Windows;

namespace PadForge.Views
{
    public partial class ProfileDialog : Window
    {
        public string ProfileName => NameBox.Text?.Trim();
        public string ExecutableNames => ExeBox.Text?.Trim();

        public ProfileDialog()
        {
            InitializeComponent();
            NameBox.Focus();
            NameBox.SelectAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Focus();
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
