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
				return String.Join("", array.Select(x => {
					/* TODO: map these probably */
					if (x == "ID") return "ID";
					if (x == "XML") return "XML";
					if (x == "URL") return "URL";
					if (x == "HTML") return "HTML";
					if (x == "API") return "API";
					if (x == "GPO") return "GPO";
					if (x == "NUM") return "Number";
					return Char.ToUpperInvariant(x[0]) + x.Substring(1).ToLowerInvariant();
				}));
			}
		}

		/* sometimes Oracle tables are created using an int to represent a bit field */


	}
}
