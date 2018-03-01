using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace TidalCSharp {

    public class MicrosoftSQLTableScriptWriter : ITableScriptWriter {

        public string GetTableDropScriptText(string databaseName, List<TableDef> tableDefList) {
            var build = new StringBuilder();
            build.AppendLine("USE " + databaseName);
            build.AppendLine();
            build.AppendLine("GO");
            build.AppendLine();
            foreach (TableDef tableDef in tableDefList) {
                if (tableDef.TableType == "TABLE") {
                    build.AppendLine("DROP " + tableDef.TableType + " " + CleanName(tableDef.TableName));
                    build.AppendLine();
                    build.AppendLine("GO");
                    build.AppendLine();
                }
            }
            return build.ToString();
        }

        public string GetTableCreateScriptText(string databaseName, List<TableDef> tableDefList) {
            var build = new StringBuilder();
            build.AppendLine("USE " + databaseName);
            build.AppendLine();
            build.AppendLine("GO");
            build.AppendLine();
            foreach (TableDef tableDef in tableDefList) {
                switch (tableDef.TableType) {
                    case "TABLE":
                        OutputTable(build, tableDef);
                        break;
                    case "VIEW":
                        /* TODO: Warn that there's no view creation yet */
                        break;
                    default:
                        throw new ApplicationException("Invalid TableType for " + tableDef.TableName + ": " + tableDef.TableType);
                }
            }

            /* we do all foreign keys at the end so the reference tables are all created */
            OutputForeignKeys(build, tableDefList);

            return build.ToString();

        }

        private void OutputTable(StringBuilder build, TableDef tableDef) {
            build.Append("CREATE " + tableDef.TableType + " " + CleanName(tableDef.TableName) + "(");
            bool first = true;
            foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
                if (first) {
                    first = false;
                }
                else {
                    build.AppendLine(",");
                    build.Append("\t");
                }
                build.Append(CleanName(columnDef.ColumnName));
                build.Append(" ");
                build.Append(columnDef.ColumnType);
                if (columnDef.DataLength != null) {
                    build.Append("(" + columnDef.DataLength + ")");
                }
                if (columnDef.IsIdentity == true) {
                    build.Append(" IDENTITY");
                }


                /* TODO: what about multi-column primary keys? */

                if (tableDef.IndexDefMap.Values.Any(i => i.IsPrimary == true && i.ColumnDefList.Contains(columnDef) == true)) {
                    build.Append(" PRIMARY KEY");
                }

                /* TODO: Where are Unique constraints? */
                /* eg: Name nvarchar(100) NOT NULL UNIQUE NONCLUSTERED */
                //if (tableDef.IndexDefMap.Any(i => i.IsUnique == true && i.ColumnDefList.Contains(columnDef) == true)) {
                // 	build.Append (" UNIQUE");
                //}
                if (columnDef.IsNullable) {
                    /* NULL is the default creation, omit for conciseness */
                    /* TODO: verify that NULL column default is not configurable for server */
                    // build.Append(" NULL");
                }
                else {
                    build.Append(" NOT NULL");
                }
            }
            build.AppendLine("\t)");
            build.AppendLine();
            build.AppendLine("GO");
            build.AppendLine();
        }

        private void OutputForeignKeys(StringBuilder build, List<TableDef> tableDefList) {
            foreach (TableDef tableDef in tableDefList) {
                foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
                    if (columnDef.ReferencedColumnDef != null) {
                        build.AppendLine();
                        build.AppendLine("ALTER TABLE " + CleanName(tableDef.TableName));
                        build.AppendLine("\t\t ADD FOREIGN KEY (" + CleanName(columnDef.ColumnName) + ") REFERENCES "
                                         + CleanName(columnDef.ReferencedTableDef.TableName) + "(" + CleanName(columnDef.ReferencedColumnDef.ColumnName) + ")");
                        build.AppendLine();
                        build.AppendLine("GO");
                    }
                }
            }

        }

        private string CleanName(string originalIdentifierName) {
            return MicrosoftSQL.DataAccess.CleanName(originalIdentifierName);
        }

    }


}

