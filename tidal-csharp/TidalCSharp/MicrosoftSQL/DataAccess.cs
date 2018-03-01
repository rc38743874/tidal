using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace TidalCSharp.MicrosoftSQL {
    public class DataAccess {


        private static SqlCommand GetTextCommand(SqlConnection conn, string commandText) {
            return new SqlCommand(commandText, conn) { CommandType = CommandType.Text };
        }

        public static List<Dictionary<string, object>> GetRows(SqlConnection conn, string commandText) {
            var list = new List<Dictionary<string, object>>();
            using (var command = GetTextCommand(conn, commandText)) {
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        var item = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++) {
                            item[reader.GetName(i)] = reader.GetValue(i);
                        }
                        list.Add(item);
                    }
                }
            }
            return list;
        }

        public static string CleanName(string originalIdentifierName) {
            switch (originalIdentifierName.ToLowerInvariant()) {
                /* TODO: grab all the mssql keywords */
                case "from":
                case "current":
                case "select":
                case "percent":
                case "table":
                case "user":
                    return "[" + originalIdentifierName + "]";
                default:
                    return originalIdentifierName;
            }
        }
    }
}
