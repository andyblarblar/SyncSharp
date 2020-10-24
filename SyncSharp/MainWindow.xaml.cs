using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using ProtoBuf;
using SyncSharp.Common;
using SyncSharp.Common.model;
using SyncSharp.viewmodels;
using Path = System.IO.Path;

namespace SyncSharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _client = new PipeClient("syncsharp");
            Closed += (sender, args) => Disconnect(sender,null);
            _vm = new SyncViewModel();

            //Load conf from correct directory
            #if DEBUG
            _vm.Config = FileSyncUtility.LoadConfig(@"..\..\..\..\SyncSharpWorker\bin\Debug\net5.0\conf.bin");
            #else
            _vm.Config = FileSyncUtility.LoadConfig(@$"..{Path.DirectorySeparatorChar}SyncSharpWorker\conf.bin");
            #endif

            PathListView.ItemsSource = _vm.Config.Paths;
            BackupIntervalInput.Text = _vm.Config.CheckInterval.ToString();
        }

        private readonly PipeClient _client;
        private readonly SyncViewModel _vm;

        private async void Disconnect(object sender, RoutedEventArgs e)
        {
            if(!_client.IsConnected) return;//TODO doesnt actually work lmao
            await _client.Disconnect();
        }

        private void ButtonClickAddPaths(object sender, RoutedEventArgs e)
        {
            //var dialog = new OpenFileDialog { Multiselect = true, CheckFileExists = true, CheckPathExists = true };

            var dialog = new VistaOpenFileDialog { Multiselect = true, CheckFileExists = true, CheckPathExists = true };
            
            dialog.ShowDialog();

            var paths = dialog.FileNames;
            var confPaths = paths.Select(f => new FileProfile {LastSynced = DateTime.MinValue, Path = f});

            //avoid duplicate paths
            _vm.Config.Paths.AddRange(confPaths.Where(f => !_vm.Config.Paths.Contains(f)));

            RefreshListBinding();
        }

        private async void ButtonClickSendConfig(object sender, RoutedEventArgs e)
        {
            if (_vm.Config.Paths.Count < 1)
            {
                MessageBox.Show("Please enter a path.","Error",MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrEmpty(_vm.Config.SavePath))
            {
                MessageBox.Show("Please enter a backup directory.","Error",MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //Ask the user if they really want to send the new config
            var confirm = MessageBox.Show(this, "This will overwrite your old settings, are you sure?", "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if(confirm == MessageBoxResult.Cancel) return;

            //Parse the time input
            _vm.Config.CheckInterval = TimeSpan.Parse(BackupIntervalInput.Text);

            await _client.Start();

            await _client.WriteAsync(_vm.Config);

            MessageBox.Show("Successfully set config");
        }

        /// <summary>
        /// Pressing del while selecting paths deletes the paths from the config.
        /// </summary>
        private void CommandBinding_DeleteSelectedPath(object sender, ExecutedRoutedEventArgs e)
        {
            if(PathListView.SelectedItems.Count < 1) return;

            var selItems = PathListView.SelectedItems;

            foreach (var item in selItems)
            {
                _vm.Config.Paths.Remove((FileProfile) item);
            }

            RefreshListBinding();
        }

        private void RefreshListBinding()
        {
            PathListView.ItemsSource = _vm.Config.Paths;
            PathListView.Items.Refresh();
        }

        private void BackupDirBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            var res = dialog.ShowDialog(this);
            if (res ?? false)
            {
                _vm.Config.SavePath = dialog.SelectedPath;
            }
        }

        private void AddFolderBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            var res = dialog.ShowDialog(this);
            if (res ?? false)
            {
                _vm.Config.Paths.Add(new FileProfile{Path = dialog.SelectedPath, LastSynced = DateTime.MinValue});
            }

            RefreshListBinding();
        }

    }
}
