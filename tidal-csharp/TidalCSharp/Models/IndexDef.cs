using System.Collections.Generic;
using System.Text;

namespace TidalCSharp {
	public class IndexDef {

		public string IndexName {get; set; }
		public bool IsPrimary {get; set; }
		public bool IsUnique {get; set; }
		public List<ColumnDef> ColumnDefList { get; set; }

		public string ToJSONString() {
			var build = new StringBuilder();
			build.Append("{");
			build.AppendFormat("\"indexName\":\"{0}\",", this.IndexName);
			build.AppendLine();
			build.AppendFormat("\"isPrimary\":{0},", this.IsPrimary.ToString().ToLowerInvariant());
			build.AppendLine();
			build.AppendFormat("\"isUnique\":{0},", this.IsUnique.ToString().ToLowerInvariant());
			build.AppendLine();

			build.Append("{\"columnArray\":[");

			bool first = true;
			foreach (ColumnDef columnDef in this.ColumnDefList) {
				if (first == true) {
					first = false;
				}
				else {
					build.AppendLine(",");
				}
				build.AppendFormat("\"{0}\"", columnDef.ColumnName);
			}
			
			build.Append("]}");
			
			return build.ToString();

		}
		
		
	}
}