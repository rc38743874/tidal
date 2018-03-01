using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace TidalCSharp {

    public class MicrosoftSQLTableExtractor : ITableExtractor {


        private SqlConnection SqlConnection { get; set; }

        public MicrosoftSQLTableExtractor(SqlConnection connection) {
            this.SqlConnection = connection;
        }

        public void Test(SqlConnection conn) {
            // catalog, owner, table, tabletype
            //DataTable table = conn.GetSchema("MetaDataCollections");
            DataTable table = conn.GetSchema("ForeignKeys");
            int length = 25;
            foreach (DataColumn col in table.Columns) {
                Console.Write("{0,-" + length + "}", col.ColumnName);
            }
            Console.WriteLine();

            foreach (DataRow row in table.Rows) {
                foreach (DataColumn col in table.Columns) {
                    if (col.DataType.Equals(typeof(DateTime)))
                        Console.Write("{0,-" + length + ":d}", row[col]);
                    else if (col.DataType.Equals(typeof(Decimal)))
                        Console.Write("{0,-" + length + ":C}", row[col]);
                    else
                        Console.Write("{0,-" + length + "}", row[col]);
                }
                Console.WriteLine();
            }


        }


        public TableDefMap ExtractTableData() {

            SqlConnection conn = this.SqlConnection;
            Console.WriteLine("begin test");
            Test(conn);

            Console.WriteLine("Completed test");

            var tableDefMap = new TableDefMap();

            Console.WriteLine("getting tables schema");

            /* 
             * Information Schema, GetSchema differences MSSQL from MySQL:
             * Views data fails for MSSQL
             * MSSQL does not have EXTRA for columns
             * MySQL uses ulong for character field length, MSSQL uses int16
             * MySQL uses STATISTICS for indexes
             * MSSQL does not indicate unique or not unique for indexes
             * MSSQL does not use the term "Foreign Key Collections", but "ForeignKeys"
             * MSSQL foreignkeys collection does not contain most fields
             */

            /* INFORMATION_SCHEMA is kinda crap truth be told.  For SQL Server,
             * it is just as easy to write queries for exactly what we want, 
             * with the added benefit that it actually works. :)
             * 
             * We'll try to stay consistent with the field names output to match
             * what information schema does, but we'll make our own queries
             */


            string queryText = "SELECT TABLE_NAME=name FROM sys.tables;";

            foreach (var row in MicrosoftSQL.DataAccess.GetRows(conn, queryText)) {

                string tableName = (string)row["TABLE_NAME"];
                TableDef tableDef = new TableDef {
                    TableName = tableName,
                    TableType = "TABLE",
                    ColumnDefMap = new Dictionary<string, ColumnDef>(),
                    IndexDefMap = new Dictionary<string, IndexDef>(),
                    ProcedureDefMap = new Dictionary<string, ProcedureDef>(),
                    FieldDefList = new List<FieldDef>(),
                    ForeignKeyList = new List<string>(),
                    ArgumentName = Char.ToLowerInvariant(tableName[0]) + tableName.Substring(1)
                };
                tableDefMap[tableName] = tableDef;

                Console.WriteLine("Adding table " + tableName);

            }

            queryText = "SELECT TABLE_NAME=name FROM sys.views;";
            foreach (var row in MicrosoftSQL.DataAccess.GetRows(conn, queryText)) {

                string tableName = (string)row["TABLE_NAME"];
                TableDef tableDef = new TableDef {
                    TableName = tableName,
                    TableType = "VIEW",
                    ColumnDefMap = new Dictionary<string, ColumnDef>(),
                    IndexDefMap = new Dictionary<string, IndexDef>(),
                    ProcedureDefMap = new Dictionary<string, ProcedureDef>(),
                    FieldDefList = new List<FieldDef>(),
                    ForeignKeyList = new List<string>()
                };
                tableDefMap[tableName] = tableDef;

                Console.WriteLine("Adding view " + tableName);

            }

            queryText = @"SELECT TABLE_NAME = ct.name,
            COLUMN_NAME = c.name,
            CHARACTER_MAXIMUM_LENGTH = c.max_length,
            DATA_TYPE = typ.name,
            IS_NULLABLE = c.is_nullable,
            IS_IDENTITY = is_identity
            FROM sys.columns c
            INNER JOIN sys.objects ct
            ON c.object_id = ct.object_id
            INNER JOIN sys.types typ
            ON c.system_type_id = typ.system_type_id
            WHERE ct.type IN ('U', 'V')
            ORDER BY TABLE_NAME, COLUMN_NAME;";
            /*  for MySQL: queryText = "SELECT COLUMN_NAME, TABLE_NAME, CHARACTER_MAXIMUM_LENGTH, DATA_TYPE, EXTRA, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS ORDER BY TABLE_NAME ASC, ORDINAL_POSITION ASC;"; */
            foreach (var row in MicrosoftSQL.DataAccess.GetRows(conn, queryText)) {
                string columnName = (string)row["COLUMN_NAME"];
                string tableName = (string)row["TABLE_NAME"];

                Console.WriteLine("Adding column " + columnName + " from table " + tableName);

                short? dataLength;
                if (row["CHARACTER_MAXIMUM_LENGTH"] == DBNull.Value) {
                    dataLength = null;
                }
                else {
                    dataLength = (short)row["CHARACTER_MAXIMUM_LENGTH"];
                }



                /* MySQL provides IsIdentity = ((string)row["EXTRA"]).Contains("auto_increment"), */
                ColumnDef columnDef = new ColumnDef {
                    ColumnName = columnName,
                    ColumnType = (string)row["DATA_TYPE"],
                    DataLength = (ulong?)dataLength,
                    IsIdentity = (bool)row["IS_IDENTITY"],
                    IsNullable = (bool)row["IS_NULLABLE"]
                };
                Console.WriteLine(tableName);

                tableDefMap[tableName].ColumnDefMap[columnName] = columnDef;
                Console.WriteLine("Column " + columnName + " added.");
            }


            /* will get primary key and unique key, which is used for Read and ReadFor functions */
            /* will get indexes for use in ListFor functions */

            /* had QUOTENAME function for SCHEMA_NAME and TABLE_NAME which I removed,
            also removed SCHEMA_NAME = OBJECT_SCHEMA_NAME(i.[object_id]), */
            queryText = @"SELECT
            TABLE_NAME = ct.name,
            INDEX_NAME = i.name,
            PRIMARY_KEY = i.is_primary_key,
            [UNIQUE] = i.is_unique,
            COLUMN_NAME = c.Name
            FROM
            sys.indexes AS i 
            INNER JOIN 
            sys.index_columns AS ic 
            ON i.[object_id] = ic.[object_id] 
            AND i.index_id = ic.index_id
            INNER JOIN 
            sys.columns c
            ON ic.column_id = c.column_id
            AND ic.[object_id] = c.[object_id]
            INNER JOIN sys.objects ct
            ON i.object_id = ct.object_id
            WHERE ct.type IN ('U', 'V')
            ORDER BY TABLE_NAME, INDEX_NAME, ic.index_column_id;";

            foreach (var row in MicrosoftSQL.DataAccess.GetRows(conn, queryText)) {
                string tableName = (string)row["TABLE_NAME"];
                string indexName = (string)row["INDEX_NAME"];
                string columnName = (string)row["COLUMN_NAME"];

                Console.WriteLine("Adding index column " + columnName + " from index " + indexName + " on table " + tableName);

                // int ordinalPosition = (int)row["ORDINAL_POSITION"];
                /* SORT_ORDER */
                Console.WriteLine("looking for table " + tableName);
                TableDef tableDef = tableDefMap[tableName];

                TableDef table = tableDefMap[tableName];
                IndexDef indexDef = null;
                if (table.IndexDefMap.TryGetValue(indexName, out indexDef) == false) {
                    indexDef = new IndexDef {
                        IndexName = indexName,
                        // IsPrimary = (indexName == "PRIMARY"), 
                        IsPrimary = (bool)row["PRIMARY_KEY"],
                        //                      IsUnique = ((bool)row["NON_UNIQUE"]!=true),
                        IsUnique = (bool)row["UNIQUE"],
                        ColumnDefList = new List<ColumnDef>()
                    };
                    table.IndexDefMap[indexName] = indexDef;
                }

                ColumnDef columnDef = tableDef.ColumnDefMap[columnName];
                indexDef.ColumnDefList.Add(columnDef);
            }

            queryText = @"SELECT TABLE_NAME = ct.name,
            COLUMN_NAME = c.name,
            REFERENCED_TABLE_NAME = rct.name,
            REFERENCED_COLUMN_NAME = rc.name
            FROM sys.foreign_key_columns AS fkc
            INNER JOIN sys.columns c
            ON fkc.parent_object_id = c.object_id
            AND fkc.parent_column_id = c.column_id
            INNER JOIN sys.objects ct
            ON c.object_id = ct.object_id
            INNER JOIN sys.columns rc
            ON fkc.referenced_object_id = rc.object_id
            AND fkc.referenced_column_id = rc.column_id
            INNER JOIN sys.objects rct
            ON rc.object_id = rct.object_id
            WHERE ct.type IN ('U', 'V')
            ORDER BY TABLE_NAME, COLUMN_NAME;";

            foreach (var row in MicrosoftSQL.DataAccess.GetRows(conn, queryText)) {
                TableDef tableDef = tableDefMap[row["TABLE_NAME"].ToString()];
                ColumnDef columnDef = tableDef.ColumnDefMap[row["COLUMN_NAME"].ToString()];

                columnDef.ReferencedTableDef = tableDefMap[row["REFERENCED_TABLE_NAME"].ToString()];
                columnDef.ReferencedColumnDef = columnDef.ReferencedTableDef.ColumnDefMap[row["REFERENCED_COLUMN_NAME"].ToString()];
                Console.WriteLine("Adding foreign key for " + tableDef.TableName + "." + columnDef.ColumnName + " to " + columnDef.ReferencedTableDef.TableName + "." + columnDef.ReferencedColumnDef.ColumnName);
            }

			return tableDefMap;
		}
	}
}
