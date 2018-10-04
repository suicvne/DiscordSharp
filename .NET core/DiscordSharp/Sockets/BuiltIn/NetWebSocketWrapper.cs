using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordSharp.Sockets.BuiltIn
{
    public class NetWebSocketWrapper
    {
        private const int ReceiveChunkSize = 4096;

        private string _URL;
        public string URL
        {
            get { return _URL; }
            private set { }
        }

        private readonly ClientWebSocket _ws;
        private readonly Uri _uri;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="uri"></param>
        public NetWebSocketWrapper(string uri)
        {
            this._URL = uri;

            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            _uri = new Uri(uri);
            _cancellationToken = _cancellationTokenSource.Token;

            //_ws.CloseStatusDescription
            //_ws.CloseStatus.Value.ToString();
        }

        #region Public methods
        public event EventHandler<SocketMessageEventArgs> MessageReceived;
        public event EventHandler<SocketClosedEventArgs> SocketClosed;
        public event EventHandler<SocketErrorEventArgs> SocketError;
        public event EventHandler<EventArgs> SocketOpened;

        public bool IsAlive
        {
            get
            {
                return _ws != null;
            }
        }

        /// <summary>
        /// Connects to the WebSocket server.
        /// </summary>
        public void Connect()
        {
            //await _ws.ConnectAsync(_uri, _cancellationToken);
            _ws.ConnectAsync(_uri, _cancellationToken).Wait();

            SocketOpened?.Invoke(this, null);

            StartListen();
        }

        /// <summary>
        /// Close socket
        /// </summary>
        public void Close()
        {
            CallOnDisconnected("User requested to exit.");
        }

        /// <summary>
        /// Send a message to the WebSocket server.
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>success</returns>
        public async Task<bool> SendMessage(string message)
        {
            byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
            return await SendMessage(messageBuffer);
        }

        /// <summary>
        /// Sends a byte array to the connected socket
        /// </summary>
        /// <param name="buffer"></param>
        ///  <returns>success</returns>
        public async Task<bool> SendMessage(byte[] buffer)
        {
            if (_ws.State != WebSocketState.Open)
            {
                throw new Exception("Connection is not open.");
            }
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                return true;
            }
            catch (Exception exp)
            {
                CallOnDisconnected(exp.Message);
            }
            return false;
        }
        #endregion

        private async void StartListen()
        {
            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open)
                {
                    byte[] Sharedbuffer = new byte[ReceiveChunkSize];


                    using (var ms = new MemoryStream()) // auto release memory
                    {
                        WebSocketReceiveResult res;
                        do
                        {
                            res = await _ws.ReceiveAsync(Sharedbuffer, CancellationToken.None);
                            if (res.MessageType == WebSocketMessageType.Close)
                            {
                                CallOnDisconnected(null);
                                return;
                            }
                            ms.Write(Sharedbuffer, 0, res.Count);
                            // ms.Write(segment.Array, segment.Offset, res.Count);
                        }
                        while (!res.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        // Return data
                        byte[] returnBuffer = new byte[ms.Length];
                        Buffer.BlockCopy(ms.ToArray(), 0, returnBuffer, 0, (int)ms.Length);

                        string msg = Encoding.UTF8.GetString(returnBuffer);

                        // Fires the return packet in a new thread
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            SocketMessageEventArgs args = new SocketMessageEventArgs
                            {
                                Message = msg
                            };
                            MessageReceived?.Invoke(this, args);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                CallOnDisconnected(ex.Message);
            }
            finally
            {
                _ws.Dispose();
            }
        }

        private void CallOnDisconnected(string messageOverride)
        {
            try
            {
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
            }
            catch { }

            SocketClosedEventArgs args = new SocketClosedEventArgs
            {
                Reason = messageOverride != null ? messageOverride : _ws.CloseStatusDescription,
                WasClean = false,
                Code = _ws.CloseStatus != null ? (int)_ws.CloseStatus.Value : -1
            };
            SocketClosed?.Invoke(this, args);
        }
    }
}
