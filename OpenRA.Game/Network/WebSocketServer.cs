using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace OpenRA.Network
{
	/// <summary>
	/// Code taken from https://gist.github.com/wildbook/b1f9adf03a47bedd0f23fafe1287c741
	/// </summary>
	public class WebSocketServer
	{
		public readonly List<WebSocketSession> Clients = new List<WebSocketSession>();

		public event EventHandler<WebSocketSession> OnClientConnected;
		public event EventHandler<WebSocketSession> OnClientDisconnected;
		public event EventHandler<string> OnMessageReceived;

		bool listening;

		public void Close() => listening = false;

		public void Listen(int port)
		{
			if (listening)
				throw new Exception("Already listening!");

			listening = true;

			var server = new TcpListener(IPAddress.Any, port);
			server.Start();

			Console.WriteLine("WS Server - UP");

			// ThreadPool.QueueUserWorkItem(_ =>
			{
				while (listening)
				{
					var session = new WebSocketSession(server.AcceptTcpClient());
					session.HandshakeCompleted += (sender, socketSession) =>
					{
						Console.WriteLine($"{session.Id}| Handshake Valid.");
						Clients.Add(session);
					};

					session.Disconnected += (sender, socketSession) =>
					{
						Console.WriteLine($"{session.Id}| Disconnected.");
						Clients.Remove(session);

						OnClientDisconnected?.Invoke(this, session);
						session.Dispose();
					};

					session.TextMessageReceived += (sender, message) =>
					{
						Console.WriteLine(message);
						OnMessageReceived?.Invoke(sender, message);
					};

					Console.WriteLine($"{session.Id}| Connected.");
					session.Start();
					OnClientConnected?.Invoke(this, session);
				}

				server.Stop();
			} // );
		}
	}
}
