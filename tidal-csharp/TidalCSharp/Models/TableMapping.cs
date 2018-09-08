using System;
namespace TidalCSharp {
	public class TableMapping {
		public string TableName { get; set; }
		public string ObjectName { get; set; }

		public ColumnMapping[] ColumnArray { get; set; }

	}
}
