///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Text;

namespace ConsolePrinter
{
	/// <summary>
	/// ConsolePrinter - An application that prints a file to stdout or stderr.
	/// It is used for testing purposes within the Griffin+ logging subsystem.
	/// </summary>
	class Program
	{
		/// <summary>
		/// Main program.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		private static int Main(string[] args)
		{
			if (args.Length < 2)
				return PrintUsage();

			// get appropriate stream
			var stream = args[0].ToLower();
			TextWriter output;
			if (stream == "stdout") output = Console.Out;
			else if (stream == "stderr") output = Console.Error;
			else return PrintUsage();

			try
			{
				var path = args[1];
				using (FileStream fs = File.OpenRead(path))
				using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
				{
					string data = reader.ReadToEnd();
					output.Write(data);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				return 1;
			}

			output.Flush();
			return 0;
		}

		/// <summary>
		/// Prints usage information.
		/// </summary>
		/// <returns>Always 1.</returns>
		private static int PrintUsage()
		{
			Console.WriteLine("ConsolePrinter - Prints the specified file to the standard output or error stream.");
			Console.WriteLine();
			Console.WriteLine("Usage: ConsolePrinter.exe [stdout|stderr] <file>");
			Console.WriteLine();
			Console.WriteLine("The input <file> must be encoded in UTF-8.");
			Console.WriteLine();

			return 1;
		}
	}
}
