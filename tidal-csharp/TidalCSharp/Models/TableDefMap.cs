using System;
using System.Collections.Generic;
using System.Text;

namespace TidalCSharp {

	public class TableDefMap : Dictionary<string, TableDef> {
		
		public string ToJSONString() {
			StringBuilder build = new StringBuilder();
			build.Append("[");
			bool first = true;
			foreach (TableDef tableDef in this.Values) {
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

