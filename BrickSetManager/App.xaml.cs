using System.Windows;

namespace BrickSetManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize database on startup
            Database.DatabaseManager.Initialize();
        }
    }
}
