using System;
using System.Text;
using System.Runtime.Serialization;

namespace TidalCSharp {

    public class ColumnDef {

		/* name as it appears in the database */
        public string ColumnName { get; set; }

		/* name cleaned to use an MSSQL/MySQL convention instead of Oracle */
		public string CleanName { get; set; }

        public string ColumnType { get; set; }

        public ulong? DataLength { get; set; }

        public bool IsIdentity { get; set; }

        public bool IsNullable { get; set; }

		/* for Oracle compatibility, the column type in SQL Server may be int instead of bit */
		public bool ForceToBit { get; set; }

        [ObsoleteAttribute]
		public bool IsKey { get; set; }

		public TableDef ReferencedTableDef { get; set; }

		public ColumnDef ReferencedColumnDef { get; set; }
		
		public string ToJSONString() {
			var build = new StringBuilder();
			build.Append("{");
			build.AppendFormat("\"columnName\":\"{0}\",", this.ColumnName);
			build.AppendLine();
			build.AppendFormat("\"cleanName\":\"{0}\",", this.CleanName);
			build.AppendLine();
			build.AppendFormat("\"columnType\":\"{0}\",", this.ColumnType);
			build.AppendLine();
			if (this.DataLength != null) {
				build.AppendFormat("\"dataLength\":{0},", this.DataLength);
				build.AppendLine();
			}

			build.AppendFormat("\"forceToBit\":{0}", this.ForceToBit.ToString().ToLowerInvariant());
			build.AppendLine();

			build.AppendFormat("\"isIdentity\":{0},", this.IsIdentity.ToString().ToLowerInvariant());
			build.AppendLine();
			build.AppendFormat("\"isNullable\":{0}", this.IsNullable.ToString().ToLowerInvariant());

			if (this.ReferencedTableDef != null) {
				build.AppendLine(",");
				build.AppendFormat("\"foreignTable\":{0},", this.ReferencedTableDef.TableName);
				build.AppendLine();
				build.AppendFormat("\"foreignColumn\":{0}", this.ReferencedColumnDef.ColumnName);
			}
			
			build.Append("}");

			return build.ToString();
		}
		
		
	}
}