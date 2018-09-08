using System;
using System.Data;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;


/*
 * differences from MySQL:
 * MSSQL before 2016 does not have DIE, you have to do a select
 * MSSQL needs a GO after drop procedure
 * MSSQL uses @'s for parameters
 * parameters don't use IN
 * parameters OUT is after the data type
 * parameters aren't in ()'s
 * use SCOPE_IDENTITY instead of LAST_INSERT_ID
 * parameters don't need sizes unless varchar char
 * MSSQL doesn't use characteristics (e.g. MODIFIES SQL DATA, SQL SECURITY INVOKER, COMMENT 'Generated by Tidal')
 * MSSQL uses AS instead of BEGIN and GO instead of END
 * MSSQL uses TOP X instead of LIMIT X
 * IF doesn't use THEN or END IF
 * IF (@allRows) has to be IF (@allRows=1)
 * Have to split script by GO keyword, those are only for command interface I think
 */

namespace TidalCSharp {
	public class MicrosoftSQLProcedureCreator : IProcedureCreator {

		private bool use2016Formatting = false;

		private void OutputDataType(StringBuilder sb, ColumnDef columnDef) {
			if (columnDef.ForceToBit == true) {
				sb.Append("bit");
			} 
			else {
				sb.Append(columnDef.ColumnType);
				/* TODO: Probably shouldn't have a datalength for types like int, rather than hardcoding types here */
				switch (columnDef.ColumnType) {
					case "char":
					case "varchar":
						if (columnDef.DataLength != null) sb.Append("(" + columnDef.DataLength + ")");
						break;
				}
			}
		}

		private string StripKeySuffix(string name) {
            if (name.EndsWith("Key", StringComparison.InvariantCultureIgnoreCase)) {
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
				var functionalName = GetFunctionalName(tableDef);
				if (ignoreTableNameList.Contains(tableName)) continue;

				scriptText.AppendLine("/*************************************");
				scriptText.AppendLine("     " + functionalName + " procedures");
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
								scriptText.Append(GetListForProcedureText(moduleName, tableDef, indexDef.ColumnDefList.Take(endIndex+1).ToList<ColumnDef>()));
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

			string storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_Create";

            OutputDIEProcedureText(scriptText, storedProcName);
            OutputCreateProcedureText(scriptText, storedProcName);

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

                    scriptText.Append("\t@" + columnDef.CleanName + " ");
					OutputDataType(scriptText, columnDef);
					
				}
			}
			
			if (identityColumnDef != null) {
				if (firstItem == false) scriptText.Append(",");
				scriptText.AppendLine("");
                scriptText.Append("\t@" + identityColumnDef.CleanName + " ");
				OutputDataType(scriptText, identityColumnDef);
                scriptText.AppendLine(" OUT");
			}
			else {
				scriptText.AppendLine();
			}

			scriptText.AppendLine("AS");
			OutputTidalSignature(scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.Append("\tINSERT ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.Append(CleanName(tableName) + " (");

			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
				if (columnDef.IsIdentity == false) {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
                        scriptText.Append("\t\t\t");
					}
                    scriptText.Append(CleanName(columnDef.ColumnName));
				}
			}

			scriptText.AppendLine(")");
			scriptText.Append("\t\tVALUES (");


			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (columnDef.IsIdentity == false) {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
                        scriptText.Append("\t\t\t\t");
					}
					scriptText.Append("@" + columnDef.CleanName);

				}
			}
			scriptText.AppendLine(");");

			if (identityColumnDef != null) {
				scriptText.AppendLine("\tSET @" + identityColumnDef.CleanName + " = SCOPE_IDENTITY();");
			}
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();
		}

		private string GetDeleteProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> primaryKeyColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;

			string storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_Delete";
			OutputDIEProcedureText(scriptText, storedProcName);

			
            OutputCreateProcedureText(scriptText, storedProcName);
			OutputPrimaryKeyArguments(scriptText, primaryKeyColumnDefList);
            scriptText.AppendLine();
			// , OUT @rowcount int
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.Append("\tDELETE FROM ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.AppendLine(CleanName(tableDef.TableName));
			scriptText.Append("\t\tWHERE");
			OutputPrimaryKeyWhereClause(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(";");
			// scriptText.AppendLine("SET @rowcount = ROW_COUNT();");
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();
		}

		private string GetDeleteForProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> indexColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;

			string storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_DeleteFor";

			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				storedProcName += StripKeySuffix(indexColumnDef.ColumnName);
			}

            OutputDIEProcedureText(scriptText, storedProcName);
            OutputCreateProcedureText(scriptText, storedProcName);

			bool firstItem = true;
				
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				string indexColumnName = indexColumnDef.ColumnName;
				if (firstItem) { 
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append("\t@" + indexColumnName + " ");
				OutputDataType(scriptText, indexColumnDef);
			}
			// 	OUT @rowcount int

			scriptText.AppendLine();
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.Append("\tDELETE FROM ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.Append(CleanName(tableName));

			/* bool */ firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				scriptText.AppendLine("");
				if (firstItem) { 
					scriptText.Append("\t\tWHERE ");
					firstItem = false;
				}
				else {
					scriptText.Append("\t\tAND ");
				}
                scriptText.Append(CleanName(indexColumnDef.ColumnName) + " = @" + indexColumnDef.CleanName);

			}
			scriptText.AppendLine(";");
			//scriptText.AppendLine("SET @rowcount = ROW_COUNT();");
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();
		}

		private string GetReadProcedureText(string moduleName, TableDef tableDef, bool isPrimary, List<ColumnDef> indexColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			string functionalName = GetFunctionalName(tableDef);
			string storedProcName = moduleName + "_" + functionalName + "_Read";
		
			if (isPrimary == false) {
				storedProcName += "For";
				foreach (ColumnDef indexColumnDef in indexColumnDefList) {
					storedProcName += StripKeySuffix(indexColumnDef.ColumnName);
				}

			}


            OutputDIEProcedureText(scriptText, storedProcName);
            OutputCreateProcedureText(scriptText, storedProcName);

			bool firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append("\t@" + indexColumnDef.CleanName + " ");
				OutputDataType(scriptText, indexColumnDef);
			}

            scriptText.AppendLine();
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.Append("\tSELECT ");
			
			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in tableDef.ColumnDefMap.Values) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
					scriptText.Append("\t\t");
				}
                scriptText.Append(CleanName(columnDef.ColumnName));
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}

			scriptText.AppendLine("");
			scriptText.Append("\tFROM ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.Append(CleanName(tableName));

			/* bool */ firstItem = true;
			foreach (ColumnDef indexColumnDef in indexColumnDefList) {
				scriptText.AppendLine("");
				if (firstItem) {
					scriptText.Append("\tWHERE ");
					firstItem = false;
				}
				else {
					scriptText.Append("\t\tAND ");
				}
				scriptText.Append("\t" + CleanName(indexColumnDef.ColumnName) + " = @" + indexColumnDef.CleanName);
			}
			scriptText.AppendLine(";");
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();

		}


		private string GetListAllProcedureText(string moduleName, TableDef tableDef, int listAllLimit) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			var storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_ListAll";
            OutputDIEProcedureText(scriptText, storedProcName);
            OutputCreateProcedureText(scriptText, storedProcName);
			scriptText.AppendLine("\t@allRows bit");
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.AppendLine("\tif (@allRows = 1)");
			scriptText.Append("\t\tSELECT ");
			
			bool firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
					scriptText.Append("\t\t\t");
				}
                scriptText.Append(CleanName(columnDef.ColumnName));
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}

			scriptText.AppendLine("");
			scriptText.Append("\t\t\tFROM ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.AppendLine(CleanName(tableName) + ";");

			scriptText.AppendLine("\tELSE");
			scriptText.Append("\t\tSELECT ");
            scriptText.AppendLine("TOP " + listAllLimit + " ");
			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
					scriptText.Append("\t\t\t");
				}
                scriptText.Append(CleanName(columnDef.ColumnName));
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}
			scriptText.AppendLine("");
			scriptText.Append("\t\t\tFROM ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.AppendLine(CleanName(tableName) + ";");
			
			
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();

		}

		private string GetListForProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> indexColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			string storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_ListFor";
			foreach (ColumnDef columnDef in indexColumnDefList) {
				storedProcName += StripKeySuffix(columnDef.ColumnName);
			}

            OutputDIEProcedureText(scriptText, storedProcName);

			
            OutputCreateProcedureText(scriptText, storedProcName);

			bool firstItem = true;
			foreach (ColumnDef columnDef in indexColumnDefList) {
				if (firstItem) { 
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
				scriptText.Append("\t@" + columnDef.CleanName + " ");
				OutputDataType(scriptText, columnDef);
			}
			scriptText.AppendLine();
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");

			scriptText.Append("SELECT ");
			/* bool */ firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (firstItem) {
					firstItem = false;
				}
				else {
					scriptText.AppendLine(",");
				}
                scriptText.Append(CleanName(columnDef.ColumnName));
				if (columnDef.ColumnName != columnDef.CleanName) {
					scriptText.Append(" AS " + columnDef.CleanName);
				}
			}
			scriptText.AppendLine("");
			scriptText.Append("FROM ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.Append(CleanName(tableName));

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
                scriptText.Append(CleanName(indexColumnDef.ColumnName) + " = @" + indexColumnDef.CleanName);
			}
			scriptText.AppendLine(";");
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();

		}
		
		private string GetUpdateProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> primaryKeyColumnDefList) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;
			List<ColumnDef> columnDefList = tableDef.ColumnDefMap.Values.ToList();

			var storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_Update";

            OutputDIEProcedureText(scriptText, storedProcName);
            OutputCreateProcedureText(scriptText, storedProcName);


			OutputPrimaryKeyArguments(scriptText, primaryKeyColumnDefList);
			
			foreach (ColumnDef columnDef in columnDefList) {
				if (primaryKeyColumnDefList.Contains(columnDef) == false) {
					scriptText.AppendLine(",");
					scriptText.Append("\t@" + columnDef.CleanName + " ");
					OutputDataType(scriptText, columnDef);
				}
			}

            scriptText.AppendLine();
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.Append("\tUPDATE ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.AppendLine(CleanName(tableName));

			scriptText.Append("\t\tSET ");

			
			bool firstItem = true;
			foreach (ColumnDef columnDef in columnDefList) {
				if (primaryKeyColumnDefList.Contains(columnDef) == false) {
					if (firstItem) {
						firstItem = false;
					}
					else {
						scriptText.AppendLine(",");
						scriptText.Append("\t\t\t");
					}
	
                    scriptText.Append(CleanName(columnDef.ColumnName) + " = @" + columnDef.CleanName);
				}
			}
		
			
			scriptText.AppendLine();
			scriptText.Append("\t\tWHERE");
			OutputPrimaryKeyWhereClause(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(";");
			
			// 	SET @rowcount = ROW_COUNT();
			
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

			return scriptText.ToString();

		}





		private string GetUpdateKeyProcedureText(string moduleName, TableDef tableDef, List<ColumnDef> primaryKeyColumnDefList, ColumnDef keyColumnDef) {
			StringBuilder scriptText = new StringBuilder();
			
			string tableName = tableDef.TableName;

			string storedProcName = moduleName + "_" + GetFunctionalName(tableDef) + "_Update" + StripKeySuffix(keyColumnDef.CleanName);

            OutputDIEProcedureText(scriptText, storedProcName);
            OutputCreateProcedureText(scriptText, storedProcName);

			OutputPrimaryKeyArguments(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(",");
			scriptText.Append("\t@" + keyColumnDef.CleanName + " ");
			OutputDataType(scriptText, keyColumnDef);

            scriptText.AppendLine();
			scriptText.AppendLine("AS");
			OutputTidalSignature (scriptText);
			scriptText.AppendLine("\tSET NOCOUNT ON");
			scriptText.Append("\tUPDATE ");
			if (tableDef.SchemaName != "dbo") scriptText.Append(tableDef.SchemaName + ".");
			scriptText.AppendLine(CleanName(tableName));

            scriptText.AppendLine("\t\tSET " + CleanName(keyColumnDef.ColumnName) + " = @" + keyColumnDef.CleanName);
			scriptText.Append("\t\tWHERE");
			OutputPrimaryKeyWhereClause(scriptText, primaryKeyColumnDefList);
			scriptText.AppendLine(";");

			// 	SET @rowcount = ROW_COUNT();
			scriptText.AppendLine("GO");
            scriptText.AppendLine();

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
				sb.Append("\t@" + primaryColumnDef.CleanName + " ");
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
                sb.Append(" " + CleanName(primaryColumnDef.ColumnName) + "=@" + primaryColumnDef.CleanName);
			}
		}

        private void OutputDIEProcedureText(StringBuilder sb, string procedureName) {
			/* MSSQL2016 allows DIE, but not before */
			if (use2016Formatting == true) {
				sb.AppendLine("DROP PROCEDURE IF EXISTS {procedureName};");
			}
            else {
                sb.AppendLine($"IF EXISTS(SELECT name FROM sysobjects WHERE name = '{procedureName}' AND xtype='P')");
                sb.AppendLine($"\tDROP PROCEDURE {procedureName}");
            }
            sb.AppendLine("GO");
            sb.AppendLine();
        }

        private void OutputCreateProcedureText(StringBuilder sb, string procedureName) {
            sb.AppendLine($"CREATE PROCEDURE {procedureName}");
        }

        private string CleanName(string originalIdentifierName) {
            return MicrosoftSQL.DataAccess.CleanName(originalIdentifierName);
        }

		private void OutputTidalSignature (StringBuilder sb) {
			sb.AppendLine ("\t-- Generated by Tidal");
		}

		private string GetFunctionalName(TableDef tableDef) {
			return NameMapping.GetFunctionalName(tableDef);
		}

		

	}

}