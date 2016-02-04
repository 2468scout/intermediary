using System;
using System.Diagnostics;
using System.IO;

namespace FRCData
{
	public class Linux
	{
		internal static string[] Execute(string exe, string args)
		{
			ProcessStartInfo oInfo = new ProcessStartInfo(exe, args);
			oInfo.UseShellExecute = false;
			oInfo.CreateNoWindow = true;

			oInfo.RedirectStandardOutput = true;
			oInfo.RedirectStandardError = true;

			StreamReader srOutput = null;
			StreamReader srError = null;

			Process proc = System.Diagnostics.Process.Start(oInfo);
			proc.WaitForExit();
			srOutput = proc.StandardOutput;
			StandardOutput = srOutput.ReadToEnd();
			//Console.WriteLine (StandardOutput);
			srError = proc.StandardError;
			StandardError = srError.ReadToEnd();
			//Console.WriteLine ("err "+StandardError);
			int exitCode = proc.ExitCode;
			proc.Close();

			return new string[]{StandardOutput, StandardError};
		}

		internal static string StandardOutput
		{
			get;
			private set;
		}
		internal static string StandardError
		{
			get;
			private set;
		}
	}
}

