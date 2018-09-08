using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace TidalCSharp {
	public static class DeOracle {

		public static string Clean(string oracleName) {

			bool hasLowercase = new Regex("[a-z]").Match(oracleName).Success;

			var array = oracleName.Split('_');
			if (hasLowercase) {
				return String.Join("", array.Select(x => Char.ToUpperInvariant(x[0]) + x.Substring(1)));
			} else {
				return String.Join("", array.Select(x => Char.ToUpperInvariant(x[0]) + x.Substring(1).ToLowerInvariant()));
			}
		}

		/* sometimes Oracle tables are created using an int to represent a bit field */


	}
}
