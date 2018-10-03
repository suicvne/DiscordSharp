using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordSharp.Sockets
{
    public class SocketMessageEventArgs : EventArgs
    {
        public string Message { get; internal set; }
    }

    public class SocketClosedEventArgs : EventArgs
    {
        public string Reason { get; internal set; }
        public int Code { get; internal set; }
        public bool WasClean { get; internal set; }
    }

    public class SocketErrorEventArgs : EventArgs
    {
        public Exception Exception { get; internal set; }
        public string Message { get; internal set; }
    }


}
