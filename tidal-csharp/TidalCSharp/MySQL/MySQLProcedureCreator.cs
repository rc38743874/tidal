using System;
using System.Data;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MySql.Data;
using MySql.Data.MySqlClient;


namespace TidalCSharp {
	public class MySQLProcedureCreator : IProcedureCreator {
		
		private void OutputDataType(StringBuilder sb, ColumnDef columnDef) {
			sb.Append(columnDef.ColumnType);
			if (columnDef.DataLength != null) sb.Append("(" + columnDef.DataLength + ")");
		}

		private string StripKeySuffix(string name) {
			if (name.EndsWith("Key")) {
				return name.Substring(0, name.Length-3);
			}
			else {
				return name;
			}
		}

		public string GetStoredProcedureScriptText(string moduleName, List<TableDef> tableDefList, int listAllLimit, List<string> ignoreTableNameList) {
			var scriptText = new StringBuilder();
			
			scriptText.AppendLine("/*  Tidal procedures for module: " + moduleName + " */");
			scriptText.AppendLine("");
			scriptText.AppendLine("");
			var orderedList = tableDefList.OrderBy(x => x.SchemaName != "dbo").ThenBy(x => x.SchemaName).ThenBy(x => x.TableName).ToList();
			foreach (TableDef tableDef in orderedList) {
				var tableName = tableDef.TableName;
				if (ignoreTableNameList.Contains(tableName)) continue;
				scriptText.AppendLine("/*************************************");
				scriptText.AppendLine("     " + tableName + " procedures");
				scriptText.AppendLine("*************************************/");

				if (tableDef.TableType == "TABLE") {

					scriptText.Append(GetCreateProcedureText(moduleName, tableDef));
					
					foreach (IndexDef indexDef in tableDef.IndexDefMap.Values) {

						if (indexDef.IsPrimary == true) {

							scriptText.Append(GetDeleteProcedureText(moduleName, tableDef, indexDef.ColumnDefList));
							
						}
						else {

							scriptText.Append(GetDeleteForProcedureText(moduleName, tableDef, indexDef.ColumnDefList));

						} // isPrimary

						if (indexDef.IsPrimary==true || indexDef.IsUnique==true) {

							scriptText.Append(GetReadProcedureText(moduleName, tableDef, indexDef.IsPrimary, indexDef.ColumnDefList));


						}

					} // for IndexDef

				} // if type = table

				scriptText.Append(GetListAllProcedureText(moduleName, tableDef, listAllLimit));

				/* It is debatable whether we should also do ListFor's for foreign keys, but really
				 * if the user hasn't indexed it, then it's not something they want to retrieve by.  Doing 
				 * a table scan (I think it would be) will be very inefficient anyway.  So for now
				 * we are going to opt against having ListFor procedures for foreign key constraints if
				 * they are not indexed.
				 *  
				 * Primary keys with more than one field you should be able to get a list for everything 
				 * at the beginning of the list sequence.
				 * 
				 */
				List<ColumnDef> primaryKeyList = null;
				foreach (IndexDef indexDef in tableDef.IndexDefMap.Values) {
					if (indexDef.IsPrimary == true) {
						primaryKeyList = indexDef.ColumnDefList;
						if (primaryKeyList.Count > 1) {
							for (int endIndex = 0; endIndex < primaryKeyList.Count - 1; endIndex++) {
								scriptText.Append(GetListForProcedureText(moduleName, tableDef, indexDef.ColumnDefList.Take(endIndex + 1).ToList<ColumnDef>()));
							}
						}
					}
					else {
						if (indexDef.IsUnique == false) {
							scriptText.Append(GetListForProcedureText(moduleName, tableDef, indexDef.ColumnDefList));
						}
					}
				} // for each indexDef

				if (tableDef.TableType == "TABLE") {
					if (primaryKeyList != null) {
						/* only make an update if there are more columns than primary ones, otherwise nothing to update */
						if (tableDef.ColumnDefMap.Count > primaryKeyList.Count) {
							scriptText.Append(GetUpdateProcedureText(moduleName, tableDef, primaryKeyList));
						}
					}
				} // if table



				foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
					if (columnDef.ReferencedTableDef != null) {
						if (primaryKeyList != null) {
							scriptText.Append(GetUpdateKeyProcedureText(moduleName, tableDef, primaryKeyList, columnDef));
						}
					}
				}

			} // for each tableDef

			return scriptText.ToString();

		}

		private string GetCreateProcedureText(string moduleName, TableDef tableDef) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			string storedProcName = moduleName + "_" + tableDef.CleanName + "_Create";


			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.Append("CREATE PROCEDURE " + storedProcName + " (");

			ColumnDef identityColumnDef = null;
			
			bool firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (columnDef.IsIdentity == true) {
					identityColumnDef = columnDef;	
				}
				else {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
					}

					scriptText.Append("IN _" + columnDef.CleanName + " ");
					OutputDataType(scriptText, columnDef);
					
				}
			}
			
			if (identityColumnDef != null) {
				if (firstItem == false) scriptText.Append(",");
				scriptText.AppendLine("");
				scriptText.Append("OUT _" + identityColumnDef.CleanName + " ");
				OutputDataType(scriptText, identityColumnDef);
			}

			scriptText.AppendLine(")");
			scriptText.AppendLine("MODIFIES SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");
			scriptText.AppendLine("INSERT " + tableName + " (");

			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
				if (columnDef.IsIdentity == false) {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
					}
					scriptText.Append(columnDef.ColumnName);
				}
			}

			scriptText.AppendLine(")");
			scriptText.AppendLine("VALUES (");


			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (columnDef.IsIdentity == false) {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
					}
					scriptText.Append("_" + columnDef.CleanName);
				}
			}
			scriptText.AppendLine(");");

			if (identityColumnDef != null) {
				scriptText.AppendLine("SET _" + identityColumnDef.CleanName + " = LAST_INSERT_ID();");
			}
			scriptText.AppendLine("END;");

			return scriptText.ToString();
		}

		private string GetDeleteProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> primaryKeyColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;

			string storedProcName = moduleName + "_" + tableDef.CleanName + "_Delete";

			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.AppendLine("CREATE PROCEDURE " + storedProcName + " (");
			OutputPrimaryKeyArguments(scriptText, primaryKeyColumnDefList);
			// , OUT _rowcount int
			scriptText.AppendLine(")");
			scriptText.AppendLine("MODIFIES SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");
			scriptText.AppendLine("DELETE FROM " + tableName);
			scriptText.AppendLine("WHERE");
			OutputPrimaryKeyWhereClause(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(";");
			// scriptText.AppendLine("SET _rowcount = ROW_COUNT();");
			scriptText.AppendLine("END;");

			return scriptText.ToString();
		}

		private string GetDeleteForProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> indexColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;

			string storedProcName = moduleName + "_" + tableDef.CleanName + "_DeleteFor";

			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				storedProcName += StripKeySuffix(indexColumnDef.ColumnName);
			}

			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.Append("CREATE PROCEDURE " + storedProcName + "(");

			bool firstItem = true;
				
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				string indexColumnName = indexColumnDef.ColumnName;
				if (firstItem) { 
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append("IN _" + indexColumnName + " ");
				OutputDataType(scriptText, indexColumnDef);
			}
			// 	OUT _rowcount int
			scriptText.AppendLine(")");
			scriptText.AppendLine("MODIFIES SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");
			scriptText.Append("DELETE FROM " + tableName);

			/* bool */ firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				scriptText.AppendLine("");
				if (firstItem) { 
					scriptText.Append("WHERE ");
					firstItem = false;
				}
				else {
					scriptText.Append("AND ");
				}
				scriptText.Append(indexColumnDef.ColumnName + " = _" + indexColumnDef.ColumnName);

			}
			scriptText.AppendLine(";");
			//scriptText.AppendLine("SET _rowcount = ROW_COUNT();");
			scriptText.AppendLine("END;");
			return scriptText.ToString();
		}

		private string GetReadProcedureText(string moduleName, TableDef tableDef, bool isPrimary, List<ColumnDef> indexColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			
			string storedProcName = moduleName + "_" + tableDef.CleanName + "_Read";
		
			if (isPrimary == false) {
				storedProcName += "For";
				foreach (ColumnDef indexColumnDef in indexColumnDefList) {
					storedProcName += StripKeySuffix(indexColumnDef.ColumnName);
				}

			}


			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.AppendLine("CREATE PROCEDURE " + storedProcName + " (");

			bool firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");

				}
				scriptText.Append("IN _" + indexColumnDef.ColumnName + " ");
				OutputDataType(scriptText, indexColumnDef);
			}
		
			scriptText.AppendLine(")");

			scriptText.AppendLine("READS SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal, SingleRow'");
			scriptText.AppendLine("BEGIN");
			scriptText.Append("SELECT ");
			
			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append(columnDef.ColumnName);
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}

			scriptText.AppendLine("");
			scriptText.Append("FROM " + tableName);
			/* bool */ firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				scriptText.AppendLine("");
				if (firstItem) {
					scriptText.Append("WHERE ");
					firstItem = false;
				}
				else {
					scriptText.Append("AND ");
				}
				scriptText.Append(indexColumnDef.ColumnName + " = _" + indexColumnDef.ColumnName);
			}
			scriptText.AppendLine(";");
			scriptText.AppendLine("END;");

			return scriptText.ToString();

		}


		private string GetListAllProcedureText(string moduleName, TableDef tableDef, int listAllLimit) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			var storedProcName = moduleName + "_" + tableDef.CleanName + "_ListAll";
			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.AppendLine("CREATE PROCEDURE " + storedProcName + " (_allRows bit)");
			scriptText.AppendLine("READS SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");
			scriptText.AppendLine("	if (_allRows) THEN");
			scriptText.Append("		SELECT ");
			
			bool firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append(columnDef.ColumnName);
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}

			scriptText.AppendLine("");
			scriptText.Append("FROM " + tableName + ";");
			scriptText.AppendLine("	ELSE");
			scriptText.Append("SELECT ");
			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append(columnDef.ColumnName);
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}
			scriptText.AppendLine("");
			scriptText.AppendLine("FROM " + tableName);
			scriptText.AppendLine("LIMIT " + listAllLimit + ";");
			scriptText.AppendLine("	END IF;");
			scriptText.AppendLine("END;");

			return scriptText.ToString();

		}

		private string GetListForProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> indexColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			string storedProcName = moduleName + "_" + tableDef.CleanName + "_ListFor";
			foreach (ColumnDef columnDef in indexColumnDefList) {
				storedProcName += StripKeySuffix(columnDef.ColumnName);
			}

			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.AppendLine("CREATE PROCEDURE " + storedProcName + " (");

			bool firstItem = true;
			foreach (ColumnDef columnDef in indexColumnDefList) {
				if (firstItem) { 
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append("IN _" + columnDef.ColumnName + " ");
				OutputDataType(scriptText, columnDef);
			}
		
			scriptText.AppendLine(")");
			scriptText.AppendLine("READS SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");

			scriptText.Append("SELECT ");
			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append(columnDef.ColumnName);
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}
			scriptText.AppendLine("");
			scriptText.Append("FROM " + tableName);
			/* bool */ firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				scriptText.AppendLine("");
				if (firstItem) {
					scriptText.Append("WHERE ");
					firstItem = false;
				}
				else {
					scriptText.Append("AND ");
				}
				scriptText.Append(indexColumnDef.ColumnName + " = _" + indexColumnDef.ColumnName);
			}
			scriptText.AppendLine(";");
			scriptText.AppendLine("END;");

			return scriptText.ToString();

		}
		
		private string GetUpdateProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> primaryKeyColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			var storedProcName = moduleName + "_" + tableDef.CleanName + "_Update";
			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.Append("CREATE PROCEDURE " + storedProcName + " (");

			OutputPrimaryKeyArguments(scriptText, primaryKeyColumnDefList);
			
			foreach (ColumnDef columnDef in columnDefList) {
				if (primaryKeyColumnDefList.Contains(columnDef) == false) {
					scriptText.AppendLine(",");
					scriptText.Append("IN _" + columnDef.ColumnName + " ");
					OutputDataType(scriptText, columnDef);
				}
			}
			
			scriptText.AppendLine(")");
			scriptText.AppendLine("MODIFIES SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");
			scriptText.AppendLine("UPDATE " + tableName);

			scriptText.Append("SET ");

			
			bool firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (primaryKeyColumnDefList.Contains(columnDef) == false) {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
					}
	
					scriptText.Append(columnDef.ColumnName + " = _" + columnDef.ColumnName);
				}
			}
		
			
			scriptText.AppendLine();
			scriptText.AppendLine("WHERE");
			OutputPrimaryKeyWhereClause(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(";");
			
			// 	SET _rowcount = ROW_COUNT();
			
			scriptText.AppendLine("END;");
			return scriptText.ToString();

		}





		private string GetUpdateKeyProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> primaryKeyColumnDefList, ColumnDef keyColumnDef) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;

			string storedProcName = moduleName + "_" + tableDef.CleanName + "_Update" + StripKeySuffix(keyColumnDef.ColumnName);

			scriptText.AppendLine("DROP PROCEDURE IF EXISTS " + storedProcName + ";");
			scriptText.AppendLine("CREATE PROCEDURE " + storedProcName + " (");
			OutputPrimaryKeyArguments(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(",");
			scriptText.Append("IN _" + keyColumnDef.ColumnName + " ");
			OutputDataType(scriptText, keyColumnDef);
			scriptText.AppendLine(")");

			scriptText.AppendLine("MODIFIES SQL DATA");
			scriptText.AppendLine("SQL SECURITY INVOKER");
			scriptText.AppendLine("COMMENT 'Generated by Tidal'");
			scriptText.AppendLine("BEGIN");
			scriptText.AppendLine("UPDATE " + tableName);

			scriptText.AppendLine("SET " + keyColumnDef.ColumnName + " = _" + keyColumnDef.ColumnName);
			scriptText.AppendLine("WHERE");
			OutputPrimaryKeyWhereClause(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(";");

			// 	SET _rowcount = ROW_COUNT();
			scriptText.AppendLine("END;");
			return scriptText.ToString();

		}

		

		private void OutputPrimaryKeyArguments(StringBuilder sb, List<ColumnDef> primaryKeyColumnDefList) {
			bool firstItem = true;
			foreach (var primaryColumnDef in primaryKeyColumnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					sb.AppendLine(",");
				}
				sb.Append("\t IN _" + primaryColumnDef.ColumnName + " ");
				OutputDataType(sb, primaryColumnDef);	
			}
		}

		private void OutputPrimaryKeyWhereClause(StringBuilder sb, List<ColumnDef> primaryKeyColumnDefList) {
			bool firstItem = true;
			foreach (var primaryColumnDef in primaryKeyColumnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					sb.AppendLine(" AND");
				}
				sb.Append(" " + primaryColumnDef.ColumnName + "=_" + primaryColumnDef.ColumnName);
			}
		}

	}

}