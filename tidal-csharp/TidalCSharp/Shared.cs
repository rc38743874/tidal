using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidalCSharp {
	class Shared {

		public static bool Verbose;

		public static void Warning(string message) {
			var oldColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"Warning: {message}");
			Console.ForegroundColor = oldColor;
		}

		public static void Info(string message) {
			if (Shared.Verbose) {
				Console.WriteLine(message);
			}
		}

	}
}
