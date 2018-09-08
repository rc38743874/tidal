using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace TidalCSharp {
	public class TableDef {

		/* name as it appears in the database */
		public string TableName { get; set; }

		/* name cleaned to use an MSSQL/MySQL convention instead of Oracle */
		public string CleanName { get; set; }

		public string SchemaName { get; set; }
		
		public string TableType {get; set; }

		public string ArgumentName {get; set;}

		[IgnoreDataMember]
		public Dictionary<string, ColumnDef> ColumnDefMap { get; set; }
		
		[IgnoreDataMember]
		public Dictionary<string, IndexDef> IndexDefMap { get; set; }

		[DataMember (Name="columnArray")]
		public ColumnDef[] GetColumnArray {
			get {
				return this.ColumnDefMap.Values.ToArray<ColumnDef>();
			}	
		}
		
		[DataMember (Name="columnArray")]
		public ColumnDef[] SetColumnDefMapFromJSON {
			set { 
				this.ColumnDefMap = new Dictionary<string, ColumnDef>();
				foreach (var columnDef in value) {
					this.ColumnDefMap[columnDef.ColumnName] = columnDef;
				}
			}
		}
		
		[DataMember (Name="indexArray")]
		public IndexDef[] GetIndexArray {
			get {
				return this.IndexDefMap.Values.ToList<IndexDef>().ToArray<IndexDef>();
			}	
		}
		
		[DataMember (Name="indexArray")]
		public IndexDef[] SetIndexDefMapFromJSON {
			set { 
				this.IndexDefMap = new Dictionary<string, IndexDef>();
				foreach (var indexDef in value) {
					this.IndexDefMap[indexDef.IndexName] = indexDef;
				}
			}
		}

		public Dictionary<string, ProcedureDef> ProcedureDefMap {get; set; }

		/* Fields that return from stored procedures usually reference tables and columns.  This is a list of all fields that 
			reference this table. */
		public List<FieldDef> FieldDefList {get; set; }

		public List<string> ForeignKeyList { get; set; }

		public string ToJSONString() {
			var build = new StringBuilder();
			build.Append("{");
			build.AppendFormat("\"tableName\":\"{0}\",", this.TableName);
			build.AppendLine();
			build.AppendFormat("\"cleanName\":\"{0}\",", this.CleanName);
			build.AppendLine();
			build.AppendFormat("\"tableType\":\"{0}\",", this.TableType);
			build.AppendLine();
			
			build.Append("{\"columnArray\":[");
			
			bool first = true;
			foreach (ColumnDef columnDef in this.ColumnDefMap.Values) {
				if (first == true) {
					first = false;
				}
				else {
					build.AppendLine(",");
				}
				build.Append(columnDef.ToJSONString());
			}
			
			build.Append("],");
			build.AppendLine("{\"indexArray\":[");
			first = true;
			foreach (IndexDef indexDef in this.IndexDefMap.Values) {
				if (first == true) {
					first = false;
				}
				else {
					build.AppendLine(",");
				}
				build.Append(indexDef.ToJSONString());
			}
			build.Append("]}");
			
			return build.ToString();
			
		}
		

	}
}