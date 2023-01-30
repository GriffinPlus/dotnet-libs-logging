///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

using BenchmarkDotNet.Running;

namespace Benchmark.Elasticsearch
{

	class Program
	{
		/// <summary>
		/// The program's entry point.
		/// </summary>
		private static void Main()
		{
			// Benchmarks targeting specific methods
			// -----------------------------------------------------------------------------------------------------------------
			BenchmarkRunner.Run(typeof(Benchmarks));

			/*
			var benchmarks = new Benchmarks();
			benchmarks.GlobalSetup();
			benchmarks.Process(1000000);
			benchmarks.GlobalCleanup();
			*/

			// -----------------------------------------------------------------------------------------------------------------
			Console.WriteLine();
			Console.WriteLine("Press any key to continue...");
			Console.ReadKey();
		}
	}

}
