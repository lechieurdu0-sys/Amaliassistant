using System;
using System.Windows;

namespace GameOverlay.UpdateInstaller
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length < 4)
            {
                MessageBox.Show(
                    "Usage: UpdateInstaller.exe <patchUrlOrPath> <appDir> <exePath> <newVersion>",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var mainWindow = new MainWindow();
            mainWindow.Initialize(e.Args[0], e.Args[1], e.Args[2], e.Args[3]);
            mainWindow.Show();
        }
    }
}

