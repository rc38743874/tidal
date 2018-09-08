using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace TidalCSharp {

	public class TableDefMap : Dictionary<string, TableDef> {
		
		public string ToJSONString() {
			StringBuilder build = new StringBuilder();
			build.Append("[");
			bool first = true;
			var sortedArray = this.Values.OrderBy(x => x.CleanName);
			foreach (TableDef tableDef in sortedArray) {
				if (first == true) {
					first = false;
				}
				else {
					build.AppendLine(",");
				}
			
				build.Append(tableDef.ToJSONString());
			}
			build.Append("]");		

			return build.ToString();
			
		}
		
		
	}
	
}

