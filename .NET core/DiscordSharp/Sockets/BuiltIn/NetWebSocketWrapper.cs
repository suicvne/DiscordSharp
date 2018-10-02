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

        private readonly ClientWebSocket _ws;
        private readonly Uri _uri;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _cancellationToken;

        private Action<NetWebSocketWrapper> _onConnected;
        private Action<string, NetWebSocketWrapper> _onMessage;

        /// <summary>
        /// CloseStatus.Value.ToString()
        /// CloseStatusDescription
        /// Socket
        /// </summary>
        private Action<int, string, NetWebSocketWrapper> _onDisconnected;

        protected NetWebSocketWrapper(string uri)
        {
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            _uri = new Uri(uri);
            _cancellationToken = _cancellationTokenSource.Token;

            //_ws.CloseStatusDescription
            //_ws.CloseStatus.Value.ToString();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="uri">The URI of the WebSocket server.</param>
        /// <returns></returns>
        public static NetWebSocketWrapper Create(string uri)
        {
            return new NetWebSocketWrapper(uri);
        }

        /// <summary>
        /// Connects to the WebSocket server.
        /// </summary>
        /// <returns></returns>
        public NetWebSocketWrapper Connect()
        {
            ConnectAsync();
            return this;
        }

        /// <summary>
        /// Set the Action to call when the connection has been established.
        /// </summary>
        /// <param name="onConnect">The Action to call.</param>
        /// <returns></returns>
        public NetWebSocketWrapper OnConnect(Action<NetWebSocketWrapper> onConnect)
        {
            _onConnected = onConnect;
            return this;
        }

        /// <summary>
        /// Set the Action to call when the connection has been terminated.
        /// </summary>
        /// <param name="onDisconnect">The Action to call</param>
        /// <returns></returns>
        public NetWebSocketWrapper OnDisconnect(Action<int, string, NetWebSocketWrapper> onDisconnect)
        {
            _onDisconnected = onDisconnect;
            return this;
        }

        /// <summary>
        /// Set the Action to call when a messages has been received.
        /// </summary>
        /// <param name="onMessage">The Action to call.</param>
        /// <returns></returns>
        public NetWebSocketWrapper OnMessage(Action<string, NetWebSocketWrapper> onMessage)
        {
            _onMessage = onMessage;
            return this;
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

        private void ConnectAsync()
        {
            //await _ws.ConnectAsync(_uri, _cancellationToken);
            _ws.ConnectAsync(_uri, _cancellationToken).Wait();
            CallOnConnected();
            StartListen();
        }

        private async void StartListen()
        {
            var buffer = new byte[ReceiveChunkSize];

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
                            _onMessage?.Invoke(msg, this);
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

            _onDisconnected?.Invoke(_ws.CloseStatus != null ? (int)_ws.CloseStatus.Value : -1,
                messageOverride != null ? messageOverride : _ws.CloseStatusDescription,
            this);
        }

        private void CallOnConnected()
        {
            _onConnected?.Invoke(this);
        }
    }
}
