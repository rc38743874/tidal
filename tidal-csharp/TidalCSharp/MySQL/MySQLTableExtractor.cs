using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Collections.Generic;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace TidalCSharp {

	public class MySQLTableExtractor : ITableExtractor {

		
		private MySqlConnection mySqlConnection { get; set; }

		public MySQLTableExtractor(MySqlConnection connection) {
			this.mySqlConnection = connection;
		}

		/* TODO: Combine commonality with MSSQL's, and include its name mapping functionality */
		public TableDefMap ExtractTableData(List<TableMapping> tableMappingList, bool cleanOracle) {

			MySqlConnection conn = this.mySqlConnection;

			var tableDefList = new TableDefMap();

			//			DataTable table = conn.GetSchema("MetaDataCollections");

			DataTable tablesData = conn.GetSchema("tables", new string[] { null, null, null, null });
			//DataTable table = conn.GetSchema("UDF");
			// DisplayData(table);

			Shared.Info("Table count: " + tablesData.Rows.Count);
			

			foreach (System.Data.DataRow row in tablesData.Rows) {
		
				string tableName = (string)row["TABLE_NAME"];
				string cleanName = tableName;
				if (cleanOracle == true) {
					cleanName = DeOracle.Clean(tableName);
				}
				TableDef tableDef = new TableDef {TableName = tableName,
					TableType = "TABLE",
					CleanName = cleanName,
					ColumnDefMap = new Dictionary<string, ColumnDef>(),
					IndexDefMap = new Dictionary<string, IndexDef>(),
					ProcedureDefMap = new Dictionary<string, ProcedureDef>(),
					FieldDefList = new List<FieldDef>(),
					ForeignKeyList = new List<string>(),
					ArgumentName = Char.ToLowerInvariant(tableName[0]) + tableName.Substring(1)
				};
				tableDefList[tableName] = tableDef;

				Shared.Info("Adding table " + tableName);

			}

			DataTable viewsData = conn.GetSchema("views", new string[] { null, null, null, null });
			//DataTable table = conn.GetSchema("UDF");
			// DisplayData(table);

			Shared.Info("View count: " + viewsData.Rows.Count);


			/*
	foreach (System.Data.DataColumn col in viewsData.Columns) {
		Shared.Info(col.ColumnName);
	}
	*/
			

			foreach (System.Data.DataRow row in viewsData.Rows) {
		
				string tableName = (string)row["TABLE_NAME"];
				string cleanName = tableName;
				if (cleanOracle == true) {
					cleanName = DeOracle.Clean(tableName);
				}
				TableDef tableDef = new TableDef {TableName = tableName,
					CleanName = cleanName,
					TableType = "VIEW",
					ColumnDefMap = new Dictionary<string, ColumnDef>(),
					IndexDefMap = new Dictionary<string, IndexDef>(),
					ProcedureDefMap = new Dictionary<string, ProcedureDef>(),
					FieldDefList = new List<FieldDef>(),
					ForeignKeyList = new List<string>()
					
				};
				tableDefList[tableName] = tableDef;

				Shared.Info("Adding view " + tableName);

			}




			// var columnRowArray = sql.getDataRows("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='" + databaseName + "' ORDER BY TABLE_NAME ASC, ORDINAL_POSITION ASC");
			DataTable columnsData = conn.GetSchema("columns", new string[] { null, null, null, null });
			foreach (System.Data.DataRow row in columnsData.Rows) {

				string columnName = (string)row["COLUMN_NAME"];
				string tableName = (string)row["TABLE_NAME"];

				Shared.Info("Adding column " + columnName + " from table " + tableName);

				
				ulong? dataLength;
				if (row["CHARACTER_MAXIMUM_LENGTH"] == DBNull.Value) {
					dataLength = null;
				}
				else {
					dataLength = (ulong)row["CHARACTER_MAXIMUM_LENGTH"];
				}

				string cleanName = columnName;
				if (cleanOracle == true) {
					cleanName = DeOracle.Clean(columnName);
				}
				ColumnDef columnDef = new ColumnDef {ColumnName = columnName,
					CleanName = cleanName,
					ColumnType = (string)row["DATA_TYPE"],
					DataLength = dataLength,
					IsIdentity = ((string)row["EXTRA"]).Contains("auto_increment"),
					IsNullable = (string)row["IS_NULLABLE"] == "YES"
				};



				tableDefList[tableName].ColumnDefMap[columnName] = columnDef;


			}


			/* will get primary key and unique key, which is used for Read and ReadFor functions
			also foreign key constraints, but we don't really use those at this point (we could potentially, to map within our object structdefs
	 */
			//	"select * FROM TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA='" + databaseName + "';"
			// var indexRowArray = sql.getDataRows("SELECT * FROM KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA='" + databaseName + "' ORDER BY CONSTRAINT_NAME ASC, ORDINAL_POSITION ASC");

			/* will get indexes for use in ListFor functions */
		
			//	var indexRowArray = sql.getDataRows("SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE table_schema = '" + databaseName + "' ORDER BY table_name, index_name, seq_in_index;");

			DataTable indexData = conn.GetSchema("Indexes", new string[] { null, null, null, null });

			Shared.Info("Index count: " + indexData.Rows.Count);
			foreach (System.Data.DataRow row in indexData.Rows) {

				string tableName = (string)row["TABLE_NAME"];
				string indexName = (string)row["INDEX_NAME"];

				Shared.Info("Adding index " + indexName + " from table " + tableName);

				TableDef table = tableDefList[tableName];

				table.IndexDefMap[indexName] = new IndexDef {IndexName = indexName,
					IsPrimary = (indexName == "PRIMARY"), 
					//						IsUnique = ((bool)row["NON_UNIQUE"]!=true),
					IsUnique = (bool)row["UNIQUE"],
					ColumnDefList = new List<ColumnDef>()
				};


			}

			DataTable indexColumnsData = conn.GetSchema("IndexColumns", new string[] { null, null, null, null });
			foreach (System.Data.DataRow row in indexColumnsData.Rows) {
				string tableName = (string)row["TABLE_NAME"];
				string indexName = (string)row["INDEX_NAME"];
				string columnName = (string)row["COLUMN_NAME"];

				Shared.Info("Adding index column " + columnName + " from index " + indexName + " on table " + tableName);

				// int ordinalPosition = (int)row["ORDINAL_POSITION"];
				/* SORT_ORDER */

				TableDef tableDef = tableDefList[tableName];
				ColumnDef columnDef = tableDef.ColumnDefMap[columnName];
				IndexDef indexDef = tableDef.IndexDefMap[indexName];
				indexDef.ColumnDefList.Add(columnDef);
			}


			DataTable foreignKeyColumnsData = conn.GetSchema("Foreign Key Columns");
			foreach (System.Data.DataRow row in foreignKeyColumnsData.Rows) {
				TableDef tableDef = tableDefList[row["TABLE_NAME"].ToString()];
				ColumnDef columnDef = tableDef.ColumnDefMap[row["COLUMN_NAME"].ToString()];

				columnDef.ReferencedTableDef = tableDefList[row["REFERENCED_TABLE_NAME"].ToString()];
				columnDef.ReferencedColumnDef = columnDef.ReferencedTableDef.ColumnDefMap[row["REFERENCED_COLUMN_NAME"].ToString()];

			}

			return tableDefList;
		}
	}
}
