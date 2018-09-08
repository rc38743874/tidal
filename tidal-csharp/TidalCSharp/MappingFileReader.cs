using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.IO;

namespace TidalCSharp {
	public static class MappingFileReader {


		public static List<TableMapping> ReadFromFile(string fileName) {
			var array = JArray.Parse(File.ReadAllText(fileName));
			List<TableMapping> returnList = array.ToObject<List<TableMapping>>();
			return returnList;
		}
	}
}
