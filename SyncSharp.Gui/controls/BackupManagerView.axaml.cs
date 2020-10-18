using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SyncSharp.Gui.controls
{
    public class BackupManagerView : UserControl
    {
        public BackupManagerView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
