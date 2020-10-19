using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;

namespace SyncSharp.Common
{
    public class PipeClient : IDisposable
    {
        private readonly NamedPipeClientStream _client;

        private readonly MemoryStream _buffer;

        public bool IsConnected => _client.IsConnected;

        public PipeClient(string pipeName)
        {
            _client = new NamedPipeClientStream(".",pipeName, PipeDirection.Out);
            _buffer = new MemoryStream();
        }

        /// <summary>
        /// Connects to the server if not currently connected.
        /// </summary>
        public async Task Start(CancellationToken token = default)
        {
            if (!IsConnected)
            {
                await _client.ConnectAsync(token);
            }
        }

        /// <summary>
        /// Proto-buf serializes an object and writes it to the pipe.
        /// </summary>
        public async Task WriteAsync<T>(T obj)
        {
            Serializer.Serialize(_buffer, obj);

            var buffer2 = _buffer.ToArray();

            await _client.WriteAsync(buffer2);

            //Reset buffer for reuse
            _buffer.SetLength(0);
        }

        /// <summary>
        /// Writes the disconnect bit to the pipe.
        /// </summary>
        public async Task Disconnect()
        {
            await _client.WriteAsync(new byte[] {1});
        }

        public void Dispose()
        {
            _client?.Dispose();
            _buffer?.Dispose();
        }
    }
}
