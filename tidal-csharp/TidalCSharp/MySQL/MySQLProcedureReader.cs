using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace TidalCSharp {

	public class MySQLProcedureReader : IProcedureReader {

		private MySqlConnection mySqlConnection { get; set; }
		
		public MySQLProcedureReader(MySqlConnection connection) {
			this.mySqlConnection = connection;
		}
		
		
		public List<ProcedureDef> MakeProcedureDefList(string databaseName, 
			string moduleName, 
			Dictionary<string, TableDef> tableDefMap) {
				
			/* 	var tableMap = {};
		var functionMap = {};
		var tableArray = [];

			
	*/
			MySqlConnection conn = this.mySqlConnection;
			
			var globalProcedureDefMap = new Dictionary<string, ProcedureDef>();

			using (MySqlCommand command = new MySqlCommand() {
				CommandText = "SELECT * FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA='" + databaseName 
					+ "' AND ROUTINE_NAME LIKE '" + moduleName + "_%_%' AND ROUTINE_TYPE='PROCEDURE';",
				CommandType = CommandType.Text,
				Connection = conn}) {
				using (var reader = command.ExecuteReader()) {
					while (reader.Read()) {
						IDataRecord procRow = reader;
						string procedureName = (string)procRow["ROUTINE_NAME"];

						if ((string)procRow["SQL_DATA_ACCESS"] == "CONTAINS SQL") {
							/* TODO: I could envision a get server time function that doesn't read or write data but should be part of a DataAccess class. */
							Console.WriteLine("Skipping procedure " + procedureName + " marked CONTAINS SQL, as this implies it does not read or write data.");
						}
						else {

							int lastUnderscoreIndex = procedureName.IndexOf("_", moduleName.Length + 2);

							string tableName = procedureName.Substring(moduleName.Length + 1, lastUnderscoreIndex - moduleName.Length - 1);
							string catalogName = (string)procRow["ROUTINE_CATALOG"];

							if (catalogName != "def") throw new ApplicationException("Unexpected catalog name found: " + catalogName);


							if (tableDefMap.ContainsKey(tableName) == false) {
								throw new ApplicationException("Table "+ tableName + " referenced in stored procedure " + procedureName + " was not found in table definitions.");
							}
							TableDef tableDef = tableDefMap[tableName];

							ProcedureDef procedureDef = new ProcedureDef {
								ProcedureName = procedureName, 
								TableDef = tableDef,
								ParameterDefMap = new Dictionary<string, ParameterDef>(),
								FieldDefMap = new Dictionary<string, FieldDef>()

								/* ReadOnly, OutputsRows */
							};

							/* TODO: there may be some issues here if we require list and read functions to a data access level of reads sql data, i think
						it may change the transaction scope if you mix with modifies sql data (not sure) */
							switch ((string)procRow["SQL_DATA_ACCESS"]) {
								case "READS SQL DATA":
									procedureDef.ReadOnly = true;
									procedureDef.OutputsRows = true;
									break;
								case "MODIFIES SQL DATA":
									procedureDef.ReadOnly = false;
									procedureDef.OutputsRows = false;
									break;
								default:
									throw new ApplicationException("Unrecognized SQL Data Access setting for procedure " + procedureName + ": " + procRow["SQL_DATA_ACCESS"]);
							}


							tableDef.ProcedureDefMap[procedureName] = procedureDef;
							globalProcedureDefMap[procedureName] = procedureDef;
						}
					}

				}

			}



			PopulateParameters(databaseName, moduleName, globalProcedureDefMap);


			/*
			 * call each procedure to see the result set
			 *
			 */



			foreach (ProcedureDef procedureDef in globalProcedureDefMap.Values) {
				if (procedureDef.OutputsRows) {
					MySqlTransaction trans = this.mySqlConnection.BeginTransaction();

					using (MySqlCommand command = new MySqlCommand() {
						CommandText = procedureDef.ProcedureName,
						CommandType = CommandType.StoredProcedure,
						Connection = this.mySqlConnection}) {

						foreach (ParameterDef parameterDef in procedureDef.ParameterDefMap.Values) {

							ParameterDirection direction;
							if (parameterDef.IsOutParameter) {
								direction = ParameterDirection.Output;
							}
							else {
								direction = ParameterDirection.Input;
							}

							var parameter = new MySqlParameter { ParameterName = parameterDef.ParameterName,
								Direction = direction,
								Size = parameterDef.ParameterSize,
								MySqlDbType = GetMySqlDbType(parameterDef.ParameterDataTypeCode)};


							/* 				for (Parameter p : proc.parameters) {
						p.sqlType.setTestValue(cs, p.procParamName);
					}
	*/
							parameter.Value = DBNull.Value;

							command.Parameters.Add(parameter);
						}




						/* alternatively for MSSQL at least: 

	SELECT COLUMN_NAME
	FROM   
	INFORMATION_SCHEMA.COLUMNS 
	WHERE   
	TABLE_NAME = 'vwGetData' 
	ORDER BY 
	ORDINAL_POSITION ASC; 

	*/
						// cs.setMaxRows(0);


						DataTable tableSchema;

						Dictionary<string, string> fieldTypeLookup = new Dictionary<string, string>();

						using (var reader = command.ExecuteReader(CommandBehavior.SchemaOnly)) {
							reader.Read();
							tableSchema = reader.GetSchemaTable();
							for (int i=0;i<reader.FieldCount;i++) {
								fieldTypeLookup[reader.GetName(i)] = reader.GetDataTypeName(i);
							}
						}
						trans.Rollback();


						foreach (DataRow row in tableSchema.Rows) {

							/* IsIdentity */
							/* 
							int columnSize = (int)row["ColumnSize"];
							string sqlTypeCode = 
								bool isReadOnly = (bool)row["IsReadOnly"];
							bool isPrimaryKey = (bool)row["IsKey"];
							bool isAutoIncrement = (bool)row["IsAutoIncrement"];
							*/

							string fieldName = row["ColumnName"].ToString();
							string baseTableName = row["BaseTableName"].ToString();
							string baseColumnName = row["BaseColumnName"].ToString();

							bool isNullable = (bool)row["AllowDBNull"];

							string dataTypeCode = TypeConvertor.ConvertSQLToCSharp(fieldTypeLookup[fieldName].ToLowerInvariant());

							/* TODO: This check wouldn't really be necessary if we could rely on GetSchemaTable's DataType field */
							if (tableDefMap.ContainsKey(baseTableName)) {
								if (tableDefMap[baseTableName].ColumnDefMap.ContainsKey(baseColumnName)) {
									string newDataTypeCode = tableDefMap[baseTableName].ColumnDefMap[baseColumnName].ColumnType;
									if (newDataTypeCode != dataTypeCode) {
										Console.WriteLine("Warning:  GetTableSchema reported an incorrect data type of " + dataTypeCode + " from stored procedure " + procedureDef.ProcedureName + ", field " + fieldName + ", instead of the source table's column data type of " + newDataTypeCode + ".");
										dataTypeCode = newDataTypeCode;
									}
								}
							}

							FieldDef fieldDef = new FieldDef {
								FieldName = fieldName,
								ProcedureDef = procedureDef,
								DataTypeCode = dataTypeCode,
								IsNullable = isNullable,
								BaseTableName = baseTableName,
								BaseColumnName = baseColumnName
							};


							procedureDef.FieldDefMap[fieldDef.FieldName] = fieldDef;


						}

					}
				}
			}

			return globalProcedureDefMap.Values.ToList<ProcedureDef>();

		}

		private void PopulateParameters(string databaseName, string moduleName, Dictionary<string, ProcedureDef> globalProcedureDefMap) {
			

			using (MySqlCommand command = new MySqlCommand() {
				CommandText = "SELECT * FROM INFORMATION_SCHEMA.PARAMETERS WHERE SPECIFIC_CATALOG='def' AND SPECIFIC_SCHEMA='" 
					+ databaseName + "' AND SPECIFIC_NAME like '" 
					+ moduleName + "_%_%' ORDER BY ORDINAL_POSITION ASC",
				CommandType = CommandType.Text,
				Connection = this.mySqlConnection}) {
				using (var reader = command.ExecuteReader()) {
					while (reader.Read()) {



						var parameterRow = reader;


						string procedureName = (string)parameterRow["SPECIFIC_NAME"];
						ProcedureDef procedureDef = globalProcedureDefMap[procedureName];

						string parameterName = (string)parameterRow["PARAMETER_NAME"];


						int? charLength;
						if (parameterRow["CHARACTER_MAXIMUM_LENGTH"] == DBNull.Value) {
							charLength = null;
						}
						else {
							charLength = (int) parameterRow["CHARACTER_MAXIMUM_LENGTH"];
						}

						int? scale;
						if (parameterRow["NUMERIC_SCALE"]==DBNull.Value) {
							scale = null;
						}
						else {
							scale = (int) parameterRow["NUMERIC_SCALE"];
						}


						ulong? precision;
						if (parameterRow["NUMERIC_PRECISION"]==DBNull.Value) {
							precision = null;
						}
						else {
							precision = (ulong) parameterRow["NUMERIC_PRECISION"];
						}



						/* try to find the best guess as to the field it may be */
						ColumnDef columnDef;
						procedureDef.TableDef.ColumnDefMap.TryGetValue(parameterName.Substring(1), out columnDef);


						ParameterDef parameterDef = new ParameterDef {
							ProcedureDef = procedureDef,
							ParameterName = parameterName,
							ParameterMode = (string)parameterRow["PARAMETER_MODE"],
							ParameterDataTypeCode = (string)parameterRow["DATA_TYPE"],
							ColumnDef = columnDef,
							CharLength = charLength,
							Precision = precision,
							Scale = scale,
							OrdinalPosition = (int)parameterRow["ORDINAL_POSITION"],
							IsOutParameter = parameterRow["PARAMETER_MODE"].ToString() == "OUT"
						};

						procedureDef.ParameterDefMap[parameterName] = parameterDef;

						/* isNullable, isOut, length */
					}
				}
			}



		}


		private MySqlDbType GetMySqlDbType (string dataTypeCode) {
			switch (dataTypeCode) {
				case "bigint":
					return MySqlDbType.Int64;
				case "int":
					return MySqlDbType.Int32;
				case "bit":
					return MySqlDbType.Bit;
				case "varchar":
					return MySqlDbType.VarChar;
				case "datetime":
					return MySqlDbType.DateTime;



				default:
					throw new ApplicationException("Unknown dataTypeCode: " + dataTypeCode);
			}


		}
	}
}

