//-----------------------------------------------------------------------------
// Filename: SIPWebSocketChannel.cs
//
// Description: Channel for transmitting SIP over a Web Socket communications layer as
// per RFC7118.
//
// Note: At the time of writing the .Net Code Libraries (CoreFX) did have some rudimentary
// Web Socket support. Unfortunately the support was limited to some Web Socket Protocol
// classes and a Web Socket Client. There was no Web Socekt Server. For the latter it
// is likely the decision was to rely on support in Kestrel and IIS.
//
// Author(s):
// Aaron Clauson
//
// History:
// 17 Oct 2005	Aaron Clauson	Created (aaron@sipsorcery.com), Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SIPSorcery.SIP
{
    /// <summary>
    ///  A SIP transport Channel for transmitting SIP over a Web Socket communications layer as per RFC7118.
    ///  
    /// <code>
    /// var sipTransport = new SIPTransport();
    /// var wsSipChannel = new SIPWebSocketChannel(IPAddress.Loopback, 80);
    ///
    /// var wssCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2("localhost.pfx");
    /// var wssSipChannel = new SIPWebSocketChannel(IPAddress.Loopback, 433, wssCertificate);
    ///
    /// sipTransport.AddSIPChannel(wsSipChannel);
    /// sipTransport.AddSIPChannel(wssSipChannel);
    /// </code>
    /// </summary>
    public class SIPWebSocketChannel : SIPChannel
    {
        public const string SIP_Sec_WebSocket_Protocol = "sip"; // Web socket protocol string for SIP as defined in RFC7118.

        /// <summary>
        /// The web socket server instantiates an instance of this class for each web socket client that connects. The methods 
        /// in this class are responsible for translating the SIP transport send and receives to and from the web socket server.
        /// </summary>
        private class SIPMessagWebSocketBehavior : WebSocketBehavior
        {
            internal SIPWebSocketChannel Channel;
            internal ILogger Logger;
            private SIPProtocolsEnum _sipProtocol;
            private IPEndPoint _remoteEndPoint;

            public Action<string> OnClientClose;

            public SIPMessagWebSocketBehavior()
            {
                base.Protocol = SIP_Sec_WebSocket_Protocol;
            }

            protected override void OnOpen()
            {
                Logger.LogDebug($"SIPMessagWebSocketBehavior.OnOpen.");

                _sipProtocol = this.Context.IsSecureConnection ? SIPProtocolsEnum.wss : SIPProtocolsEnum.ws;
                _remoteEndPoint = this.Context.UserEndPoint;

                Channel.AddClientConnection(this.ID, this);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                Logger.LogDebug($"SIPMessagWebSocketBehavior.OnMessage: bytes received {e.Data?.Length}.");

                if (e.RawData?.Length > 0)
                {
                    // TODO: Check what happens if web socket server asked to listen on IPAddress.Any.
                    Channel.SIPMessageReceived?.Invoke(Channel, Channel.ListeningSIPEndPoint, new SIPEndPoint(_sipProtocol, _remoteEndPoint, Channel.ID, this.ID), e.RawData);
                }
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Logger.LogDebug($"SIPMessagWebSocketBehavior.OnClose: reason {e.Reason}, was clean {e.WasClean}.");
                OnClientClose?.Invoke(this.ID);
            }

            protected override void OnError(ErrorEventArgs e)
            {
                Logger.LogDebug($"SIPMessagWebSocketBehavior.OnError: reason {e.Message}.");
            }

            public void Send(byte[] buffer, int offset, int length)
            {
                base.Send(buffer.Skip(offset).Take(length).ToArray());
            }
        }

        /// <summary>
        /// This object is reponsible for all the web sockets magic including accepting HTTP requests, matching URLs, handling the
        /// keep alives etc. etc. Any data messages received by the server will be handed over to the SIP transport layer for processing.
        /// </summary>
        private WebSocketServer m_webSocketServer;

        /// <summary>
        /// Maintains a list of current web socket connections across for this web socket server. This allows the SIP transport
        /// layer to quickly match a channel where the same connection must be re-used.
        /// </summary>
        private ConcurrentDictionary<string, SIPMessagWebSocketBehavior> m_clientConnections = new ConcurrentDictionary<string, SIPMessagWebSocketBehavior>();

        /// <summary>
        /// Creates a SIP channel to listen for and send SIP messages over a web socket communications layer.
        /// </summary>
        /// <param name="endPoint">The IP end point to listen on and send from.</param>
        public SIPWebSocketChannel(IPEndPoint endPoint, X509Certificate2 certificate)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException("endPoint", "The end point must be specified when creating a SIPTCPChannel.");
            }

            ID = Crypto.GetRandomInt(CHANNEL_ID_LENGTH).ToString();
            ListeningIPAddress = endPoint.Address;
            Port = endPoint.Port;
            IsReliable = true;

            if (certificate == null)
            {
                SIPProtocol = SIPProtocolsEnum.ws;
                m_webSocketServer = new WebSocketServer(endPoint.Address, endPoint.Port, false);
            }
            else
            {
                SIPProtocol = SIPProtocolsEnum.wss;
                m_webSocketServer = new WebSocketServer(endPoint.Address, endPoint.Port, true);
                var sslConfig = m_webSocketServer.SslConfiguration;
                sslConfig.ServerCertificate = certificate;
                sslConfig.CheckCertificateRevocation = false;
                IsSecure = true;
            }

            //m_webSocketServer.Log.Level = WebSocketSharp.LogLevel.Debug;

            logger.LogInformation($"SIP WebSocket Channel created for {endPoint}.");

            m_webSocketServer.AddWebSocketService<SIPMessagWebSocketBehavior>("/", (behaviour) =>
            {
                behaviour.Channel = this;
                behaviour.Logger = this.logger;

                behaviour.OnClientClose += (id) => m_clientConnections.TryRemove(id, out _);
            });

            m_webSocketServer.Start();
        }

        public SIPWebSocketChannel(IPAddress listenAddress, int listenPort)
            : this(new IPEndPoint(listenAddress, listenPort), null)
        { }

        /// <summary>
        /// Creates a new secure web socket server (e.g. wss://localhost).
        /// </summary>
        /// <param name="listenAddress">The IPv4 or IPv6 address to listen on.</param>
        /// <param name="listenPort">The network port to listen on.</param>
        /// <param name="certificate">The X509 certificate to supply to connecting clients. Unless
        /// the client has been specifically configured otherwise the it will perform validation on the certificate
        /// which typically involved checking that the hostname of the server matches the certificate's common name.</param>
        public SIPWebSocketChannel(IPAddress listenAddress, int listenPort, X509Certificate2 certificate)
            : this(new IPEndPoint(listenAddress, listenPort), certificate)
        { }

        /// <summary>
        /// Records a new client connection in the list. This allows responses or subsequent requests to the same SIP agent
        /// to reuse the same connection.
        /// </summary>
        /// <param name="id">The unique ID of the client connection.</param>
        /// <param name="client">The web socket client.</param>
        private void AddClientConnection(string id, SIPMessagWebSocketBehavior client)
        {
            m_clientConnections.TryAdd(id, client);
        }

        /// <summary>
        /// Ideally sends on the web socket channel should specify the connection ID. But if there's
        /// a good reason not to we can check if there is an existing client connection with the
        /// requested remote end point and use it.
        /// </summary>
        /// <param name="destinationEndPoint">The remote destiation end point to send the data to.</param>
        /// <param name="buffer">The data to send.</param>
        /// <returns>If no errors SocketError.Success otherwise an error value.</returns>
        public override async void Send(IPEndPoint destinationEndPoint, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await SendAsync(destinationEndPoint, buffer);
        }

        /// <summary>
        /// Ideally sends on the web socket channel should specify the connection ID. But if there's
        /// a good reason not to we can check if there is an existing client connection with the
        /// requested remote end point and use it.
        /// </summary>
        /// <param name="destinationEndPoint">The remote destiation end point to send the data to.</param>
        /// <param name="buffer">The data to send.</param>
        /// <returns>If no errors SocketError.Success otherwise an error value.</returns>
        public override async void Send(IPEndPoint destinationEndPoint, byte[] buffer)
        {
            if (destinationEndPoint == null)
            {
                throw new ApplicationException("An empty destination was specified to Send in SIPWebSocketChannel.");
            }
            else if (buffer == null || buffer.Length == 0)
            {
                throw new ArgumentException("buffer", "The buffer must be set and non empty for Send in SIPWebSocketChannel.");
            }

            await SendAsync(destinationEndPoint, buffer);
        }

        /// <summary>
        /// Ideally sends on the web socket channel should specify the connection ID. But if there's
        /// a good reason not to we can check if there is an existing client connection with the
        /// requested remote end point and use it.
        /// </summary>
        /// <param name="destinationEndPoint">The remote destiation end point to send the data to.</param>
        /// <param name="buffer">The data to send.</param>
        /// <returns>If no errors SocketError.Success otherwise an error value.</returns>
        public override async Task<SocketError> SendAsync(IPEndPoint destinationEndPoint, byte[] buffer)
        {
            if (destinationEndPoint == null)
            {
                throw new ApplicationException("An empty destination was specified to Send in SIPWebSocketChannel.");
            }
            else if (buffer == null || buffer.Length == 0)
            {
                throw new ArgumentException("buffer", "The buffer must be set and non empty for Send in SIPWebSocketChannel.");
            }

            try
            {
                var client = m_clientConnections.Where(x => x.Value.Context.UserEndPoint.Equals(destinationEndPoint)).Select(x => x.Value).FirstOrDefault();

                if (client != null)
                {
                    await Task.Run(() => client.Send(buffer, 0, buffer.Length));
                    return SocketError.Success;
                }
                else
                {
                    return SocketError.ConnectionReset;
                }
            }
            catch (SocketException sockExcp)
            {
                return sockExcp.SocketErrorCode;
            }
        }

        /// <summary>
        /// Sends a SIP message asynchronously on a specific stream connection.
        /// </summary>
        /// <param name="connectionID">The ID of the specific web socket connection that the message must be sent on.</param>
        /// <param name="buffer">The data to send.</param>
        /// <returns>If no errors SocketError.Success otherwise an error value.</returns>
        public override async Task<SocketError> SendAsync(string connectionID, byte[] buffer)
        {
            if (String.IsNullOrEmpty(connectionID))
            {
                throw new ArgumentException("connectionID", "An empty connection ID was specified for a Send in SIPWebSocketChannel.");
            }
            else if (buffer == null || buffer.Length == 0)
            {
                throw new ArgumentException("buffer", "The buffer must be set and non empty for Send in SIPWebSocketChannel.");
            }

            try
            {
                SIPMessagWebSocketBehavior client = null;
                m_clientConnections.TryGetValue(connectionID, out client);

                if (client != null)
                {
                    await Task.Run(() => client.Send(buffer, 0, buffer.Length));
                    return SocketError.Success;
                }
                else
                {
                    return SocketError.ConnectionReset;
                }
            }
            catch (SocketException sockExcp)
            {
                return sockExcp.SocketErrorCode;
            }
        }

        /// <summary>
        /// Not implemented for the WebSocket channel.
        /// </summary>
        public override void Send(IPEndPoint dstEndPoint, byte[] buffer, string serverCertificateName)
        {
            throw new NotImplementedException("This Send method is not available in the SIP Web Socket channel, please use an alternative overload.");
        }

        /// <summary>
        /// Not implemented for the WebSocket channel.
        /// </summary>
        public override Task<SocketError> SendAsync(IPEndPoint dstEndPoint, byte[] buffer, string serverCertificateName)
        {
            throw new NotImplementedException("This Send method is not available in the SIP Web Socket channel, please use an alternative overload.");
        }

        /// <summary>
        /// Checks whether the web socket SIP channel has a connection matching a unique connection ID.
        /// </summary>
        /// <param name="connectionID">The connection ID to check for a match on.</param>
        /// <returns>True if a match is found or false if not.</returns>
        public override bool HasConnection(string connectionID)
        {
            return m_clientConnections.ContainsKey(connectionID);
        }

        /// <summary>
        /// Checks whether there is an existing client web socket connection for a remote end point.
        /// </summary>
        /// <param name="remoteEndPoint">The remote end point to check for an existing connection.</param>
        /// <returns>True if there is a connection or false if not.</returns>
        public override bool HasConnection(IPEndPoint remoteEndPoint)
        {
            return m_clientConnections.Any(x => x.Value.Context.UserEndPoint.Equals(remoteEndPoint));
        }

        /// <summary>
        /// Stops the web socket server and closes any client connections.
        /// </summary>
        public override void Close()
        {
            try
            {
                logger.LogDebug($"Closing SIP Web Socket Channel {ListeningIPAddress}:{Port}.");

                Closed = true;
                m_webSocketServer.Stop();
            }
            catch (Exception excp)
            {
                logger.LogWarning("Exception SIPWebSocketChannel Close. " + excp.Message);
            }
        }

        /// <summary>
        /// Calls close on the channel when it is disposed.
        /// </summary>
        public override void Dispose()
        {
            this.Close();
        }
    }
}