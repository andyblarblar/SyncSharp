using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace SyncSharp.Common.model
{
    [ProtoContract]
    public class FileProfile
    {
        [ProtoMember(1)]
        public string Path { get; set; }

        [ProtoMember(2)]
        public DateTime LastSynced { get; set; }

    }
}
