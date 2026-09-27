using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace AssemblyCSharp.Functions
{
	public class GClass150
	{
		[CompilerGenerated]
		private sealed class Class14
		{
			public int int_0;

			public GClass150 gclass150_0;

			internal void method_0()
			{
				gclass150_0.method_4(int_0);
			}
		}

		private sealed class ReceiveState
		{
			public readonly Socket socket;

			public readonly byte[] buffer = new byte[4096];

			public readonly List<byte> pendingBytes = new List<byte>();

			public ReceiveState(Socket socket)
			{
				this.socket = socket;
			}
		}

		public bool bool_0;

		private static GClass150 gclass150_0;

		public static int int_0;

		public bool bool_1 = false;

		public bool bool_2 = false;

		private const int MaxFrameSize = 1048576;

		private readonly object sendLock = new object();

		private readonly object connectLock = new object();

		private bool connecting;

		public static Socket socket_0;

		public static GClass150 smethod_0()
		{
			return (gclass150_0 != null) ? gclass150_0 : (gclass150_0 = new GClass150());
		}

		public void method_0(int Port)
		{
			lock (connectLock)
			{
				try
				{
					if (socket_0 != null && socket_0.Connected && bool_2)
						return;
				}
				catch
				{
				}
				if (connecting)
					return;
				connecting = true;
			}

			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					while (bool_0)
					{
						try
						{
							method_4(Port);
							return;
						}
						catch (Exception ex)
						{
							BossHuntDiagnostics.Log("GAME_SOCKET", "CONNECT_RETRY", 0, "", "SOCKET", ex.GetType().Name);
							GClass149.smethod_0("Data/Errors/Connect.txt", ex.ToString());
							Thread.Sleep(1000);
						}
					}
				}
				finally
				{
					lock (connectLock)
						connecting = false;
				}
			});
			thread.IsBackground = true;
			thread.Start();
		}

		private void method_4(int port)
		{
			Socket newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				BossHuntDiagnostics.Log("GAME_SOCKET", "CONNECT_ATTEMPT", 0, "", "SOCKET", "port=" + port);
				newSocket.Connect(IPAddress.Loopback, port);
				BossHuntDiagnostics.Log("GAME_SOCKET", "CONNECTED", 0, "", "SOCKET", "port=" + port);

				Socket oldSocket = socket_0;
				socket_0 = newSocket;
				bool_2 = false;

				ReceiveState state = new ReceiveState(newSocket);
				newSocket.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, method_3, state);

				method_5(new vMessage
				{
					cmd = 0,
					data = Encoding.ASCII.GetBytes(int_0.ToString())
				});
				bool_2 = true;
				BossHuntDiagnostics.Log("GAME_SOCKET", "HANDSHAKE_SENT", 0, "", "SOCKET", "account=" + int_0);
				BossZoneScanner.Instance.SendKnownBossLocations();

				if (oldSocket != null && oldSocket != newSocket)
				{
					try
					{
						oldSocket.Shutdown(SocketShutdown.Both);
					}
					catch
					{
					}
					try
					{
						oldSocket.Close();
					}
					catch
					{
					}
				}

				Thread.Sleep(400);
			}
			catch
			{
				if (socket_0 == newSocket)
					socket_0 = null;
				bool_2 = false;
				try
				{
					newSocket.Close();
				}
				catch
				{
				}
				throw;
			}
		}

		private void method_1(string string_0)
		{
			if (bool_0)
			{
				vMessage vMessage2 = JsonConvert.DeserializeObject<vMessage>(string_0);
				if (vMessage2 != null)
				{
					if (vMessage2.cmd >= 100 && vMessage2.cmd <= 105)
					{
						BossHuntDiagnostics.Log("GAME_SOCKET", "RX_CMD", 0, "", "SOCKET", "cmd=" + vMessage2.cmd);
						BossZoneScanner.Instance.HandleManagerMessage(vMessage2.cmd, vMessage2.data);
					}
					else
						GClass171.smethod_0().method_23(vMessage2);
				}
			}
		}

		public void method_2(object obj)
		{
			try
			{
				method_5(obj);
			}
			catch (ObjectDisposedException)
			{
			}
			catch (SocketException)
			{
			}
			catch (IOException)
			{
			}
		}

		private void method_5(object obj)
		{
			string json = JsonConvert.SerializeObject(obj);
			byte[] payload = Encoding.UTF8.GetBytes(json ?? "");
			if (payload.Length > MaxFrameSize)
				throw new InvalidDataException("Manager/game frame too large: " + payload.Length);

			byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
			byte[] frame = new byte[4 + payload.Length];
			Buffer.BlockCopy(header, 0, frame, 0, 4);
			Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

			Socket socket = socket_0;
			if (socket == null)
				throw new IOException("Manager socket is not connected.");

			lock (sendLock)
			{
				int sent = 0;
				while (sent < frame.Length)
				{
					int count = socket.Send(frame, sent, frame.Length - sent, SocketFlags.None);
					if (count <= 0)
						throw new IOException("Socket closed while sending manager/game frame.");
					sent += count;
				}
			}
		}

		public void method_3(IAsyncResult ar)
		{
			ReceiveState state = ar.AsyncState as ReceiveState;
			if (state == null)
				return;

			int num;
			try
			{
				num = state.socket.EndReceive(ar);
			}
			catch
			{
				HandleConnectionClosed(state);
				return;
			}

			if (num <= 0)
			{
				HandleConnectionClosed(state);
				return;
			}

			try
			{
				for (int i = 0; i < num; i++)
					state.pendingBytes.Add(state.buffer[i]);

				string json;
				while (TryTakeFrame(state.pendingBytes, out json))
					method_1(json);

				if (state.socket == socket_0 && state.socket.Connected)
				{
					state.socket.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, method_3, state);
					return;
				}
			}
			catch (Exception ex)
			{
				GClass149.smethod_0("Data/Errors/ReceiveData.txt", ex.ToString());
			}

			HandleConnectionClosed(state);
		}

		private static bool TryTakeFrame(List<byte> pendingBytes, out string json)
		{
			json = null;
			if (pendingBytes.Count < 4)
				return false;

			byte[] header = pendingBytes.GetRange(0, 4).ToArray();
			int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
			if (length < 0 || length > MaxFrameSize)
				throw new InvalidDataException("Invalid manager/game frame length: " + length);
			if (pendingBytes.Count < 4 + length)
				return false;

			byte[] payload = pendingBytes.GetRange(4, length).ToArray();
			pendingBytes.RemoveRange(0, 4 + length);
			json = Encoding.UTF8.GetString(payload);
			return true;
		}

		private void HandleConnectionClosed(ReceiveState state)
		{
			try
			{
				state.socket.Shutdown(SocketShutdown.Both);
			}
			catch
			{
			}
			try
			{
				state.socket.Close();
			}
			catch
			{
			}

			if (state.socket != socket_0)
				return;

			socket_0 = null;
			bool_2 = false;
			BossHuntDiagnostics.Log("GAME_SOCKET", "DISCONNECTED", 0, "", "SOCKET", "");
			if (bool_0)
				method_0(GClass172.int_0);
		}
	}
}
