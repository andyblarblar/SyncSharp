using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SyncSharp.Common.model
{
    [ProtoContract]
    public class Config
    {
        /// <summary>
        /// The interval between checks on the server.
        /// </summary>
        [ProtoMember(1)]
        public TimeSpan CheckInterval { get; set; }

        /// <summary>
        /// Paths to dir or files to sync.
        /// </summary>
        [ProtoMember(2)]
        public List<FileProfile> Paths { get; set; }

        [ProtoMember(3)]
        public string SavePath { get; set; }

    }

}
