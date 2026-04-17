using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using grzyClothTool.Helpers;

namespace grzyClothTool.Views
{
    public partial class ProjectSetupDialog : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _dialogTitle = LocalizationHelper.Get("Str.ProjectSetup.Title.CreateNew");
        public string DialogTitle
        {
            get => _dialogTitle;
            set { _dialogTitle = value; OnPropertyChanged(); }
        }


        private string _projectName = string.Empty;
        public string ProjectName
        {
            get => _projectName;
            set 
            { 
                _projectName = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(ProjectExistsWarning));
                OnPropertyChanged(nameof(ShowProjectExistsWarning));
            }
        }
        public string ProjectExistsWarning =>
            LocalizationHelper.GetFormat("Str.ProjectSetup.Warning.ProjectExists", ProjectName);

        private bool _isSelfContained = true;
        public bool IsSelfContained
        {
            get => _isSelfContained;
            set 
            { 
                _isSelfContained = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExternal));
            }
        }

        public bool IsExternal
        {
            get => !_isSelfContained;
            set 
            { 
                _isSelfContained = !value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelfContained));
            }
        }

        private bool _showDrawableCount;
        public bool ShowDrawableCount
        {
            get => _showDrawableCount;
            set { _showDrawableCount = value; OnPropertyChanged(); }
        }

        private string _drawableCountMessage = string.Empty;
        public string DrawableCountMessage
        {
            get => _drawableCountMessage;
            set { _drawableCountMessage = value; OnPropertyChanged(); }
        }

        private string _confirmButtonText = LocalizationHelper.Get("Str.ProjectSetup.Button.Create");
        public string ConfirmButtonText
        {
            get => _confirmButtonText;
            set { _confirmButtonText = value; OnPropertyChanged(); }
        }

        public bool ShowProjectExistsWarning
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ProjectName))
                    return false;

                var mainFolder = PersistentSettingsHelper.Instance.MainProjectsFolder;
                if (string.IsNullOrEmpty(mainFolder))
                    return false;

                return SaveHelper.ProjectExists(mainFolder, ProjectName.Trim(), out _);
            }
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ProjectName) && 
                               ProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        public bool Confirmed { get; private set; }

        public ProjectSetupDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public static ProjectSetupDialog ShowForNewProject(Window owner)
        {
            var dialog = new ProjectSetupDialog
            {
                Owner = owner,
                DialogTitle = LocalizationHelper.Get("Str.ProjectSetup.Title.CreateNew"),
                ConfirmButtonText = LocalizationHelper.Get("Str.ProjectSetup.Button.Create"),
                IsSelfContained = true,
                ShowDrawableCount = false
            };
            
            dialog.ProjectNameTextBox.Focus();
            dialog.ShowDialog();
            return dialog;
        }

        public static ProjectSetupDialog ShowForOpenAddon(Window owner, string suggestedName, int drawableCount, int metaFileCount)
        {
            var dialog = new ProjectSetupDialog
            {
                Owner = owner,
                DialogTitle = LocalizationHelper.Get("Str.ProjectSetup.Title.OpenExisting"),
                ConfirmButtonText = LocalizationHelper.Get("Str.ProjectSetup.Button.Open"),
                ProjectName = suggestedName,
                IsSelfContained = false,
                ShowDrawableCount = true,
                DrawableCountMessage = metaFileCount > 1
                    ? LocalizationHelper.GetFormat("Str.ProjectSetup.DrawableCount.WithMeta", drawableCount, metaFileCount)
                    : LocalizationHelper.GetFormat("Str.ProjectSetup.DrawableCount.Simple", drawableCount)
            };
            
            dialog.ProjectNameTextBox.SelectAll();
            dialog.ProjectNameTextBox.Focus();
            dialog.ShowDialog();
            return dialog;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            if (ShowProjectExistsWarning)
            {
                var result = Controls.CustomMessageBox.Show(
                    LocalizationHelper.GetFormat("Str.ProjectSetup.Confirm.OverwriteMessage", ProjectName),
                    LocalizationHelper.Get("Str.ProjectSetup.Confirm.OverwriteTitle"),
                    Controls.CustomMessageBox.CustomMessageBoxButtons.YesNo,
                    Controls.CustomMessageBox.CustomMessageBoxIcon.Warning);

                if (result != Controls.CustomMessageBox.CustomMessageBoxResult.Yes)
                {
                    return;
                }
            }

            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
