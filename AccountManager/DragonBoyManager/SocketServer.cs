using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace DragonBoyManager
{
	public static class SocketServer
	{
		public class vMessage
		{
			[JsonProperty(nameof(cmd))]
			public int cmd;

			[JsonProperty(nameof(data))]
			public byte[] data;
		}

		public class vSocket
		{
			public Socket _Socket { get; set; }

			public string _Name { get; set; }

			public vSocket(Socket socket)
			{
				_Socket = socket;
			}
		}

		public static List<Account> waitingAccounts = new List<Account>();

		public static ManualResetEvent allDone = new ManualResetEvent(false);

		private static Socket _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		private static List<vSocket> __ClientSockets { get; set; } = new List<vSocket>();

		private const int MaxFrameSize = 1048576;

		public static void StartListening(int port)
		{
			Dns.GetHostEntry(Dns.GetHostName());
			IPAddress any = IPAddress.Any;
			IPEndPoint localEP = new IPEndPoint(any, port);
			Socket socket = new Socket(any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				socket.Bind(localEP);
				socket.Listen(100);
				while (true)
				{
					allDone.Reset();
					socket.BeginAccept(AcceptCallback, socket);
					allDone.WaitOne();
				}
			}
			catch
			{
				Random random = new Random();
				int num = random.Next(1000, 10000);
				if (num == port)
				{
					num = random.Next(1000, 10000);
					return;
				}
				File.WriteAllText(TabData._instance.PortPath, num.ToString());
				MessageBox.Show((MainController.language == 0) ? "Vui lòng mở lại QLTK!" : "Please reopen this application!");
				Application.Exit();
			}
		}

		private static void onMessage(vMessage msg, StateObject state)
		{
			try
			{
				switch (msg.cmd)
				{
				case 0:
				{
					int accountId = int.Parse(Encoding.ASCII.GetString(msg.data));
					Account account = null;
					if (TabData._instance != null)
						account = TabData._instance.GetAccounts().Find(acc => acc != null && acc.ID == accountId);
					if (account == null)
					{
						lock (waitingAccounts)
							account = waitingAccounts.Find(acc => acc != null && acc.ID == accountId);
					}

					state.account = account;
					if (state.account != null)
					{
						Socket oldSocket = state.account.workSocket;
						state.account.workSocket = state.workSocket;
						state.account.Status = MainController.language == 0 ? "Đã kết nối" : "Connected";

						lock (waitingAccounts)
						{
							waitingAccounts.RemoveAll(acc => acc == null || acc.ID == accountId);
							waitingAccounts.Add(state.account);
						}

						if (oldSocket != null && oldSocket != state.workSocket)
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

						if (MainController.instance != null)
							MainController.instance.REFRESH = true;
					}
					break;
				}
				case 1:
				{
					Account account = TabData._instance.GetAccounts().Find(acc => acc != null && acc.ID == int.Parse(Encoding.ASCII.GetString(msg.data)) && !string.IsNullOrEmpty(acc.Status));
					if (account != null)
						account.Status = "";
					if (MainController.instance != null)
						MainController.instance.REFRESH = true;
					break;
				}
				case 3:
				{
					int accountId = int.Parse(Encoding.ASCII.GetString(msg.data));
					Account account = null;
					if (TabData._instance != null)
						account = TabData._instance.GetAccounts().Find(acc => acc != null && acc.ID == accountId);
					if (account == null)
					{
						lock (waitingAccounts)
							account = waitingAccounts.Find(acc => acc != null && acc.ID == accountId);
					}
					if (account != null)
					{
						account.Status = MainController.language == 0 ? "Mất kết nối" : "Disconnected";
						BossHuntCoordinator.Instance.HandleDisconnected(account);
					}
					if (MainController.instance != null)
						MainController.instance.REFRESH = true;
					break;
				}
				case 2:
					break;
				case BossHuntCoordinator.CmdZone:
				case BossHuntCoordinator.CmdFound:
				case BossHuntCoordinator.CmdDead:
				case BossHuntCoordinator.CmdReady:
				case BossHuntCoordinator.CmdFailed:
					BossHuntCoordinator.Instance.HandleClientMessage(state.account, msg.cmd, msg.data);
					break;
				}
			}
			catch (Exception ex)
			{
				try
				{
					File.AppendAllText("Data/Errors/SocketOnMessage.txt", ex + Environment.NewLine);
				}
				catch
				{
				}
			}
		}

		public static void sendMessage(this Account account, vMessage msg)
		{
			if (account == null)
				return;
			Send(account.workSocket, JsonConvert.SerializeObject(msg));
		}

		public static void AcceptCallback(IAsyncResult ar)
		{
			allDone.Set();
			Socket socket = ((Socket)ar.AsyncState).EndAccept(ar);
			StateObject stateObject = new StateObject
			{
				workSocket = socket
			};
			socket.BeginReceive(stateObject.buffer, 0, stateObject.buffer.Length, SocketFlags.None, ReadCallback, stateObject);
			Send(socket, new vMessage
			{
				cmd = 0
			});
		}

		public static void ReadCallback(IAsyncResult ar)
		{
			StateObject stateObject = (StateObject)ar.AsyncState;
			Socket workSocket = stateObject.workSocket;
			int num;
			try
			{
				num = workSocket.EndReceive(ar);
			}
			catch
			{
				CloseClient(stateObject);
				return;
			}

			if (num <= 0)
			{
				CloseClient(stateObject);
				return;
			}

			try
			{
				for (int i = 0; i < num; i++)
					stateObject.pendingBytes.Add(stateObject.buffer[i]);

				string json;
				while (TryTakeFrame(stateObject.pendingBytes, out json))
				{
					vMessage msg = JsonConvert.DeserializeObject<vMessage>(json);
					if (msg == null)
						continue;
					if (msg.cmd == -1)
					{
						CloseClient(stateObject);
						return;
					}
					onMessage(msg, stateObject);
				}

				workSocket.BeginReceive(stateObject.buffer, 0, stateObject.buffer.Length, SocketFlags.None, ReadCallback, stateObject);
			}
			catch (Exception ex)
			{
				try
				{
					File.AppendAllText("Data/Errors/SocketOnMessage.txt", ex + Environment.NewLine);
				}
				catch
				{
				}
				CloseClient(stateObject);
			}
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

		private static void CloseClient(StateObject stateObject)
		{
			if (stateObject == null)
				return;

			Socket workSocket = stateObject.workSocket;
			if (workSocket != null)
			{
				try
				{
					workSocket.Shutdown(SocketShutdown.Both);
				}
				catch
				{
				}
				try
				{
					workSocket.Close();
				}
				catch
				{
				}
			}

			Account account = stateObject.account;
			if (account != null && account.workSocket == workSocket)
			{
				account.workSocket = null;
				account.Status = "";
				BossHuntCoordinator.Instance.HandleDisconnected(account);
				if (MainController.instance != null)
					MainController.instance.REFRESH = true;
			}
		}

		private static void Send(Socket handler, string data)
		{
			if (handler == null)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(data ?? "");
			if (payload.Length > MaxFrameSize)
				throw new InvalidDataException("Manager/game frame too large: " + payload.Length);

			byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
			byte[] frame = new byte[4 + payload.Length];
			Buffer.BlockCopy(header, 0, frame, 0, 4);
			Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

			lock (handler)
			{
				int sent = 0;
				while (sent < frame.Length)
				{
					int count = handler.Send(frame, sent, frame.Length - sent, SocketFlags.None);
					if (count <= 0)
						throw new IOException("Socket closed while sending manager/game frame.");
					sent += count;
				}
			}
		}

		private static void Send(Socket handler, vMessage msg)
		{
			Send(handler, JsonConvert.SerializeObject(msg));
		}
	}
}
