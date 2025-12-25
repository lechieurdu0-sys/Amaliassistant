using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using WpfMessageBox = System.Windows.MessageBox;

namespace GameOverlay.App
{
    public partial class UpdateProgressWindow : Window
    {
        private bool _isCancelled = false;
        private bool _isDownloading = false;

        public bool IsCancelled => _isCancelled;
        
        public UpdateProgressWindow()
        {
            InitializeComponent();
            this.Closing += UpdateProgressWindow_Closing;
        }

        private void UpdateProgressWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isDownloading && !_isCancelled)
            {
                var result = WpfMessageBox.Show(
                    "Le téléchargement est en cours. Êtes-vous sûr de vouloir annuler ?",
                    "Annuler la mise à jour",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _isCancelled = true;
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        public void SetStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
            });
        }

        public void SetProgress(double progress, string? details = null)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = progress;
                ProgressText.Text = $"{progress:F0}%";
                
                if (!string.IsNullOrEmpty(details))
                {
                    DetailsText.Text = details;
                }
            });
        }

        public void SetIndeterminate(bool indeterminate)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.IsIndeterminate = indeterminate;
                if (indeterminate)
                {
                    ProgressText.Text = "En cours...";
                }
            });
        }

        public void SetDownloading(bool downloading)
        {
            Dispatcher.Invoke(() =>
            {
                _isDownloading = downloading;
                CancelButton.IsEnabled = downloading;
            });
        }

        public void SetInstalling()
        {
            Dispatcher.Invoke(() =>
            {
                _isDownloading = false;
                CancelButton.IsEnabled = false;
                CancelButton.Visibility = Visibility.Collapsed;
                SetStatus("Installation de la mise à jour en cours...");
                SetIndeterminate(true);
                DetailsText.Text = "Veuillez patienter, l'application va se fermer et redémarrer automatiquement.";
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = WpfMessageBox.Show(
                "Êtes-vous sûr de vouloir annuler la mise à jour ?",
                "Annuler la mise à jour",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _isCancelled = true;
                SetStatus("Annulation en cours...");
                CancelButton.IsEnabled = false;
            }
        }
    }
}

