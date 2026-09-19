using System;
using System.IO;

namespace AssemblyCSharp.Functions
{
	public class GClass149
	{
		public static void smethod_0(string path, string message)
		{
			if (!File.Exists(path))
				File.WriteAllText(path, message);
			else if (!(File.ReadAllText(path) == message))
			{
				File.WriteAllText(path, message);
			}
		}

		public static void smethod_1(string message, int type)
		{
			if (type != 0)
			{
				if (type == 1)
					GClass73.smethod_30(message);
			}
			else
				GClass144.gclass52_0.method_7("[ThanhLc]: " + message, 0);
		}

		private static DateTime traceUntil = DateTime.MinValue;

		public static void smethod_2(string path, string message)
		{
			try
			{
				string directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);
				File.AppendAllText(path, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message + Environment.NewLine);
			}
			catch
			{
			}
		}

		public static void smethod_3()
		{
			traceUntil = DateTime.Now.AddSeconds(5.0);
			smethod_2("Data/Errors/ui_protocol.log", "NPC trace started for 5 seconds.");
		}

		public static void smethod_4(sbyte command)
		{
			if (DateTime.Now <= traceUntil)
				smethod_2("Data/Errors/ui_protocol.log", "RX command=" + command);
		}

		private static string lastDiagnosticError = "";

		public static void smethod_5(string path, string message)
		{
			if (message == lastDiagnosticError)
				return;
			lastDiagnosticError = message;
			smethod_2(path, message);
		}
	}
}
