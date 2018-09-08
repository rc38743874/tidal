using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace TidalCSharp {

	public class MicrosoftSQLProcedureReader : IProcedureReader {

		private SqlConnection SqlConnection { get; set; }

		public MicrosoftSQLProcedureReader(SqlConnection connection) {
			this.SqlConnection = connection;
		}


		public List<ProcedureDef> MakeProcedureDefList(string databaseName,
			string moduleName,
			Dictionary<string, TableDef> tableDefMap) {

			/* 	var tableMap = {};
		var functionMap = {};
		var tableArray = [];

			
	*/
			SqlConnection conn = this.SqlConnection;

			var globalProcedureDefMap = new Dictionary<string, ProcedureDef>();

			using (SqlCommand command = new SqlCommand() {

				CommandText = $@"SELECT sobj.name as Name,    
(SELECT count(is_selected) FROM sys.sql_dependencies AS sis WHERE sobj.object_id = sis.object_id AND is_selected = 1) AS PerformsSelectCount,
                (SELECT count(is_updated) FROM sys.sql_dependencies AS siu WHERE sobj.object_id = siu.object_id AND is_updated = 1) AS PerformsUpdateCount
                FROM sys.objects AS sobj WHERE type = 'P' AND name like '{moduleName}\_%\_%' ESCAPE '\' ORDER BY name;",
				CommandType = CommandType.Text,
				Connection = conn
			}) {
				using (var reader = command.ExecuteReader()) {
					while (reader.Read()) {
						IDataRecord procRow = reader;


						string procedureName = (string)procRow["Name"];
						Console.WriteLine("Reading procedure " + procedureName);

						/* we also have the ability to check referenced tables if need be */

						int secondUnderscoreIndex = procedureName.IndexOf("_", moduleName.Length + 2, StringComparison.InvariantCulture);

						string functionalName = procedureName.Substring(moduleName.Length + 1, secondUnderscoreIndex - moduleName.Length - 1);

						Console.WriteLine($"moduleName:{moduleName}, functionalName:{functionalName}, secondUnderscoreIndex:{secondUnderscoreIndex}");


						/* that is the functional name, so we want to find by that */
						var tableDef = tableDefMap.Values.SingleOrDefault(x => NameMapping.GetFunctionalName(x) == functionalName);

						if (tableDef == null) {
							throw new ApplicationException("Table with functional name " + functionalName + " referenced in stored procedure " + procedureName + " was not found in table definitions.");
						}

						ProcedureDef procedureDef = new ProcedureDef {
							ProcedureName = procedureName,
							TableDef = tableDef,
							ParameterDefMap = new Dictionary<string, ParameterDef>(),
							FieldDefMap = new Dictionary<string, FieldDef>()
						};

						Console.WriteLine($"procedure {procedureName}, select:{(int)procRow["PerformsSelectCount"]}, update:{(int)procRow["PerformsUpdateCount"]} ");

						/* DELETE and UPDATE queries will have a value in PerformsSelectCount, so we screen by PerformsUpdateCount first */
						if ((int)procRow["PerformsUpdateCount"] > 0) {
							procedureDef.ReadOnly = false;
							procedureDef.OutputsRows = false;
						}
						else {
							procedureDef.ReadOnly = true;
							procedureDef.OutputsRows = true;

							if ((int)procRow["PerformsSelectCount"] == 0) {
								throw new ApplicationException($"Procedure {procedureName} had 0 values for select and for update dependency counts");
							}
						}

						tableDef.ProcedureDefMap[procedureName] = procedureDef;
						globalProcedureDefMap[procedureName] = procedureDef;
					}

				}

			}



			PopulateParameters(databaseName, moduleName, globalProcedureDefMap);



			foreach (ProcedureDef procedureDef in globalProcedureDefMap.Values) {


				if (procedureDef.OutputsRows) {
					// Console.WriteLine("Collecting output fields from procedure " + procedureDef.ProcedureName);
					// Console.WriteLine(procedureDef.ToJSONString());

					/* MSSQL 2012 and beyond have result set function, which we'll use in this version */
//					PopulateFields2008(procedureDef);
					PopulateFields2012(procedureDef);

				}
			}

			return globalProcedureDefMap.Values.ToList<ProcedureDef>();

		}

		private void PopulateParameters(string databaseName, string moduleName, Dictionary<string, ProcedureDef> globalProcedureDefMap) {


			using (SqlCommand command = new SqlCommand() {
				CommandText = $"SELECT * FROM INFORMATION_SCHEMA.PARAMETERS WHERE SPECIFIC_CATALOG='{databaseName}' AND SPECIFIC_NAME like '{moduleName}\\_%\\_%' ESCAPE '\\' ORDER BY ORDINAL_POSITION ASC",
				CommandType = CommandType.Text,
				Connection = this.SqlConnection
			}) {
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
							charLength = (int)parameterRow["CHARACTER_MAXIMUM_LENGTH"];
						}

						int? scale;
						if (parameterRow["NUMERIC_SCALE"] == DBNull.Value) {
							scale = null;
						}
						else {
							scale = (int)parameterRow["NUMERIC_SCALE"];
						}



						byte? precision;
						if (parameterRow["NUMERIC_PRECISION"] == DBNull.Value) {
							precision = null;
						}
						else {
							precision = (byte)parameterRow["NUMERIC_PRECISION"];
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
							Precision = (ulong?)precision,
							Scale = scale,
							OrdinalPosition = (int)parameterRow["ORDINAL_POSITION"],
							IsOutParameter = parameterRow["PARAMETER_MODE"].ToString() == "INOUT"
						};

						procedureDef.ParameterDefMap[parameterName] = parameterDef;

						/* isNullable, isOut, length */
					}
				}
			}
		}




		private void PopulateFields2012(ProcedureDef procedureDef) {


			/* pass procedureName, then a string of parameters (not necessary), 
			 *      browserInfo should be 0 to return only the outputted columns and not join columns
			 *      and finally browserInfo must be 1 to get source table info on second call */
			var validNameList = new List<string>();
			using (SqlCommand command = new SqlCommand() {
				CommandText = $@"SELECT FieldName=name FROM sys.dm_exec_describe_first_result_set('{procedureDef.ProcedureName}', NULL, 0)",
				CommandType = CommandType.Text,
				Connection = this.SqlConnection
			}) {
				using (var reader = command.ExecuteReader()) {
					while (reader.Read()) {
						IDataReader row = reader;
						string fieldName = row["FieldName"].ToString();
						validNameList.Add(fieldName);
					}
				}
			}

			using (SqlCommand command = new SqlCommand() {
				CommandText = $@"SELECT FieldName=name, DataTypeCode=system_type_name, IsNullable = is_nullable, BaseTableName = source_table, BaseColumnName = source_column FROM sys.dm_exec_describe_first_result_set('{procedureDef.ProcedureName}', NULL, 1)",
				CommandType = CommandType.Text,
				Connection = this.SqlConnection
			}) {
				var regex = new Regex("^(\\w+)\\(\\d+(,\\d+)?\\)$");

				using (var reader = command.ExecuteReader()) {
					while (reader.Read()) {
						IDataReader row = reader;

						string fieldName = row["FieldName"].ToString();
						if (validNameList.Contains(fieldName) == false) {
							// Console.WriteLine("Skipping superfluous field " + fieldName);
						}
						else {
							string sqlDataTypeCode = row["DataTypeCode"].ToString();

							/* MSSQL2012 will send a char length too, e.g. varchar(10), decimal(18,0) */
							var match = regex.Match(sqlDataTypeCode);
							if (match.Success) {
								sqlDataTypeCode = match.Groups[1].Value;
							}

							string dataTypeCode;
							try {
								dataTypeCode = TypeConvertor.ConvertSQLToCSharp(sqlDataTypeCode);
							} catch (Exception ex) {
								Console.WriteLine("last call to ConvertSQLToCSharp was for procedure " + procedureDef.ProcedureName + ", field named \"" + fieldName + "\".  This can happen if you have a stored procedure that is referencing a column that no longer exists for a table.  Check that the procedure will run on its own.");
								throw;
							}

							bool isNullable = (bool)row["IsNullable"];
							/* max_length, precision, scale */

							/* TODO: using result set procedure does not populate source 
							 * table or column, which is as close to these fields 
							 * as I could find */
							string baseTableName = row["BaseTableName"].ToString();
							string baseColumnName = row["BaseColumnName"].ToString();
							// Console.WriteLine($"DEBUG:procedureName={procedureDef.ProcedureName}, fieldName={fieldName}, baseTableName={baseTableName}, baseColumnName={baseColumnName}");

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
		}

		/* TODO: GetSchemaTable was failing on SQL Server Express, not sure why.  
		 * 		It's not that it needed data rows in the table.
		 * 		Does it need data actually returned? */
		private void PopulateFields2008(ProcedureDef procedureDef) {
			/* MSSQL prior to 2012 might need this, we can verify if there's a client to test against at some point */

			/*
			 * call the procedure to see the result set
			 *
			 */



			using (SqlTransaction trans = this.SqlConnection.BeginTransaction()) {
				
				using (SqlCommand command = new SqlCommand() {
					CommandText = procedureDef.ProcedureName,
					CommandType = CommandType.StoredProcedure,
					Connection = this.SqlConnection,
					Transaction = trans
				}) {

					foreach (ParameterDef parameterDef in procedureDef.ParameterDefMap.Values) {
						ParameterDirection direction;
						if (parameterDef.IsOutParameter) {
							direction = ParameterDirection.Output;
						}
						else {
							direction = ParameterDirection.Input;
						}

						var parameter = new SqlParameter {
							ParameterName = parameterDef.ParameterName,
							Direction = direction,
							Size = parameterDef.ParameterSize,
							SqlDbType = GetSqlDbType(parameterDef.ParameterDataTypeCode)
						};

						parameter.Value = DBNull.Value;
						command.Parameters.Add(parameter);
					}

					DataTable tableSchema;

					/* we really want all 3 behaviors, and within a transaction, but the two
					 * behaviors CommandBehavior.SchemaOnly and CommandBehavior.KeyInfo fail
					 * to give us a schema table to read.  So we have to get a result within
					 * a transaction.  There's nothing super wrong with that, but it feels 
					 * dirty.  Here are the results of combinations we've tried (all with SQL
					 * Server Express 2014):
					 * No behaviors, no transaction : succeeded
					 * CommandBehavior.SchemaOnly alone, no transaction : failed
					 * CommandBehavior.SchemaOnly| CommandBehavior.KeyInfo, no trans : failed
					 * CommandBehavior.SchemaOnly| CommandBehavior.SingleResult, no trans : failed
					 * CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo | CommandBehavior.SingleResult, no trans : failed
					 * CommandBehavior.KeyInfo, no trans : failed
					 * CommandBehavior.KeyInfo | CommandBehavior.SingleResult, no trans : failed
					 * CommandBehavior.SingleResult, no trans : succeeded
					 * No behaviors, with trans : succeeded
					 * CommandBehavior.SingleResult, with trans : succeeded
					 */ 
					using (var reader = command.ExecuteReader(CommandBehavior.SingleResult)) {
						reader.Read();
						tableSchema = reader.GetSchemaTable();
					}
					trans.Rollback();

					if (tableSchema == null) throw new ApplicationException("Sampling of procedure " + procedureDef.ProcedureName + " yielded no schema.");

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

						/* TODO: on views, these BaseTableName and BaseColumnName are for the view, not the source table like we get for MSSQL2012 */
						string baseTableName = row["BaseTableName"].ToString();
						string baseColumnName = row["BaseColumnName"].ToString();
						bool isNullable = (bool)row["AllowDBNull"];


						string dataTypeCode = TypeConvertor.ConvertCLRToVernacular(row["DataType"].ToString());

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

		private SqlDbType GetSqlDbType(string dataTypeCode) {
			switch (dataTypeCode) {
				case "bigint":
					return SqlDbType.BigInt;
				case "int":
					return SqlDbType.Int;
				case "bit":
					return SqlDbType.Bit;
				case "varchar":
					return SqlDbType.VarChar;
				case "datetime":
					return SqlDbType.DateTime;
				default:
					throw new ApplicationException("Unknown dataTypeCode: " + dataTypeCode);
			}
		}

	}
}


