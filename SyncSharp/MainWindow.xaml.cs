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
using ProtoBuf;
using SyncSharp.Common.model;

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
            client = new NamedPipeClientStream(".","syncsharp",PipeDirection.Out);
        }

        private NamedPipeClientStream client;

        private void ButtonClickSendConfig(object sender, RoutedEventArgs e)
        {
            if (!client.IsConnected)
            {
                client.Connect();
            }

            var config = new Config {
                CheckInterval = TimeSpan.FromMinutes(.2),
                Paths = new List<FileProfile> { new FileProfile { LastSynced = DateTime.MinValue, Path = "D:\\music\\Donkey Kong Country Tropical Freeze Complete Soundtrack" } },
                SavePath = "C:\\Users\\Andyblarblar\\Downloads\\Backup"
            };

            using var mem = new MemoryStream();

            Serializer.Serialize(mem, config);

            var buffer = mem.ToArray();

            client.Write(buffer);
        }

    }
}
