using System;
using System.Text;
using System.Collections.Generic;

namespace TidalCSharp {
	public class MicrosoftSQLClassCreator : IClassCreator {

		
		public string GetDataAccessClassText(string projectNamespace, string modelNamespace, List<ModelDef> modelDefList) {
			StringBuilder buildText = new StringBuilder();
			buildText.AppendLine("using System;");
			buildText.AppendLine("using System.Collections.Generic;");
			buildText.AppendLine("using System.Data;");
			buildText.AppendLine("using System.Linq;");
			buildText.AppendLine("using System.Web;");
			buildText.AppendLine("using System.Data.SqlClient;");

			if (modelNamespace != "Models") {
				buildText.AppendLine("using Models = " + modelNamespace + ";");
			}



			if (projectNamespace != null) {
				buildText.AppendLine("");
				buildText.AppendLine("namespace " + projectNamespace + " {");
				buildText.AppendLine("");
			}
			buildText.AppendLine("");
			buildText.AppendLine("\tpublic static class DataAccess {");
			buildText.AppendLine("");
			buildText.AppendLine("");
			buildText.AppendLine("\t\t/* Helper functions */");
			buildText.AppendLine("\t\tpublic static SqlCommand GetCommand(SqlConnection conn, string procedureName) {");
			buildText.AppendLine("\t\t\treturn new SqlCommand(procedureName, conn) { CommandType = CommandType.StoredProcedure };");
			buildText.AppendLine("\t\t}");
			buildText.AppendLine("");
			buildText.AppendLine("");


			foreach (ModelDef modelDef in modelDefList) {

				buildText.AppendLine("\t\tpublic class " + modelDef.ModelName + " {");
				buildText.AppendLine("");
				
				foreach (FunctionDef functionDef in modelDef.FunctionDefList) {
					if (functionDef.UsesResult == true) {
						buildText.AppendLine("\t\t\tpublic class " + functionDef.FunctionName + "Result {");
						buildText.AppendLine("\t\t\t\tpublic " + functionDef.ReturnTypeCode + " " + modelDef.ModelName + " {get; set;}");
						foreach (ResultPropertyDef rpDef in functionDef.ResultPropertyDefList) {
							buildText.AppendLine("\t\t\t\tpublic " + rpDef.PropertyTypeCode + " " + rpDef.PropertyName + " {get; set;}");
						}
						buildText.AppendLine("\t\t\t}");
					}
				}


			/* make extra class if the field is not part of the model for the table, and it's not a Key-suffixed reference */
			/* in that case we make a public static class with the table name + function name + the word Result, for example AuthorCreateNameResult
				that contains all of the fields that are not part of the model.   */



				foreach (FunctionDef functionDef in modelDef.FunctionDefList) {
					if (functionDef.OutputsList == true) {
						CreateFunctionMultiRow(buildText, modelDef, functionDef);
					}
					else if (functionDef.OutputsObject == true) {
						/* TODO: if it's only a single field output, should it return just that as a return value? */
						CreateFunctionSingleRow(buildText, modelDef, functionDef);
					}
					else {
						CreateFunctionNoRows(buildText, modelDef, functionDef);
					}
					buildText.AppendLine("");
				}


				if (modelDef.UsesBuildListFunction == true) {
					AddBuildList(buildText, modelDef);
				}

				if (modelDef.UsesMakeObjectFunction == true) {
					AddMakeObject(buildText, modelDef);
				}

				buildText.AppendLine("\t\t}");
				buildText.AppendLine("");

			}

			buildText.AppendLine("\t}"); // DataAccess
			buildText.AppendLine("}"); // namespace
			return buildText.ToString();
		}


		public void CreateFunctionNoRows(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef) {
			/* version using all arguments */
			AddFunctionDeclaration(buildText, functionDef);
			AddArguments(buildText, functionDef);
			AddUsingCommand(buildText, functionDef.ProcedureDef);
			
			AddCommandParameters(buildText, functionDef);

			buildText.AppendLine("");
			buildText.AppendLine("\t\t\t\t\tcommand.ExecuteNonQuery();");
			if (functionDef.ReturnTypeCode != null) {
				buildText.AppendLine("\t\t\t\t\treturn (" + functionDef.ReturnTypeCode +")outParameter.Value;");
			}
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine("");


			/* version using object as input */
			AddFunctionDeclaration(buildText, functionDef);
			AddObjectArgument(buildText, modelDef);

			buildText.Append("\t\t\t\t");
			if (functionDef.ReturnTypeCode != null) {
				buildText.Append("return ");
			}
			buildText.AppendLine(modelDef.ModelName + "." + functionDef.FunctionName + "(conn,");
			AddFunctionArguments(buildText, modelDef, functionDef);

			buildText.AppendLine("\t\t\t}");

		}





		public void CreateFunctionMultiRow(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef) {
			AddFunctionDeclaration(buildText, functionDef);
			AddArguments(buildText, functionDef);

			AddUsingCommand(buildText, functionDef.ProcedureDef);
			AddCommandParameters(buildText, functionDef);

			if (functionDef.UsesResult == true) {
				buildText.AppendLine("\t\t\t\t\tvar list = new List<" + functionDef.FunctionName + "Result> ();");
				buildText.AppendLine("\t\t\t\t\tusing (var reader = command.ExecuteReader()) {");
				buildText.AppendLine("\t\t\t\t\t\twhile (reader.Read()) {");
				AddExtraResultFill(buildText, modelDef, functionDef);
				buildText.AppendLine("\t\t\t\t\t\t\tlist.Add(item);");
				buildText.AppendLine("\t\t\t\t\t\t}");
				buildText.AppendLine("\t\t\t\t\t}");
				buildText.AppendLine("\t\t\t\t\treturn list;");
			}
			else {
				buildText.AppendLine("\t\t\t\t\treturn BuildList(command);");
			}
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t}");

		}


		public void CreateFunctionSingleRow(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef) {
			AddFunctionDeclaration(buildText, functionDef);
			AddArguments(buildText, functionDef);
			AddUsingCommand(buildText, functionDef.ProcedureDef);
			AddCommandParameters(buildText, functionDef);
			buildText.AppendLine("\t\t\t\t\tusing (var reader = command.ExecuteReader()) {");
			buildText.AppendLine("\t\t\t\t\t\tif (reader.Read()) {");
			
			if (functionDef.UsesResult) {
				AddExtraResultFill(buildText, modelDef, functionDef);
				buildText.AppendLine("return item;");
				buildText.AppendLine("};");
			}
			
			buildText.AppendLine("\t\t\t\t\t\t\treturn MakeObject(reader, null);");
			buildText.AppendLine("\t\t\t\t\t\t} else {");
			buildText.AppendLine("\t\t\t\t\t\t\treturn null;");
			buildText.AppendLine("\t\t\t\t\t\t}");
			buildText.AppendLine("\t\t\t\t\t}");
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine("");

			/* e.g. Read from object */
			AddFunctionDeclaration(buildText, functionDef);
			AddObjectArgument(buildText, modelDef);
			buildText.AppendLine("\t\t\t\treturn " + modelDef.ModelName + "." + functionDef.FunctionName + "(conn,");
			AddFunctionArguments(buildText, modelDef, functionDef);
			buildText.AppendLine("\t\t\t}");
		}

		public void AddFunctionDeclaration(StringBuilder buildText, FunctionDef functionDef) {
			string code = "";
			if (functionDef.UsesResult) {
				code = functionDef.FunctionName + "Result";
			}
			else {
				code = functionDef.ReturnTypeCode ?? "void";
			}
			
			if (functionDef.OutputsList) code = "List<" + code + ">";
			buildText.Append("\t\t\tpublic static " + code + " " + functionDef.FunctionName + "(SqlConnection conn");
		}

		public void AddArguments(StringBuilder buildText, FunctionDef functionDef) {
			foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				buildText.AppendLine(",");
				buildText.Append("\t\t\t\t\t\t" + argumentDef.ArgumentTypeCode + (argumentDef.IsNullable ? "?" :"") + " " + argumentDef.ArgumentName);
			}
			buildText.AppendLine(") {");
			buildText.AppendLine("");

		}

		public void AddObjectArgument(StringBuilder buildText, ModelDef modelDef) {
			buildText.AppendLine(",");
			buildText.Append("\t\t\t\t\t\t");
			buildText.AppendLine("Models." + modelDef.ModelName + " inputObject) {");
			buildText.AppendLine();
		}

		public void AddFunctionArguments(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef) {

			bool firstParameter = true;
			foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				if (firstParameter) {
					firstParameter = false;
				}
				else {
					buildText.AppendLine(",");
				}

				buildText.Append("\t\t\t\t\t\t");
				PropertyDef propertyDef = argumentDef.PropertyDef;
				if (propertyDef.IsReference) {
					string subPropertyName = propertyDef.PropertyName;
					if (argumentDef.ArgumentName.EndsWith("Key")) {
						subPropertyName = propertyDef.PropertyTypeCode + "ID";
					}
					if (argumentDef.IsNullable) {
						buildText.Append("(inputObject." + propertyDef.PropertyName + " == null) ? (" + argumentDef.ArgumentTypeCode + "?)null : inputObject." + propertyDef.PropertyName + "." + subPropertyName);
					}
					else {
						buildText.Append("inputObject." + propertyDef.PropertyName + "." + subPropertyName);
					}
				}
				else {
					buildText.Append("inputObject." + propertyDef.PropertyName);
				}

			}
			buildText.AppendLine(");");
		}

		public void AddUsingCommand(StringBuilder buildText, ProcedureDef procedureDef) {
			buildText.AppendLine("\t\t\t\tusing (var command = DataAccess.GetCommand(conn, \"" + procedureDef.ProcedureName + "\")) {");
		}

		public void AddCommandParameters(StringBuilder buildText, FunctionDef functionDef) {
			foreach (ParameterDef parameterDef in functionDef.ProcedureDef.ParameterDefMap.Values) {
	//		foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				ArgumentDef argumentDef = parameterDef.ArgumentDef;
				if (parameterDef.IsOutParameter == true) {
					buildText.AppendLine("\t\t\t\t\tvar outParameter = new SqlParameter { ParameterName = \"" + parameterDef.ParameterName + "\", Direction = ParameterDirection.Output, Size = " + parameterDef.ParameterSize + ", MicrosoftSQLDbType = MicrosoftSQLDbType." + GetMicrosoftSQLDbTypeCode(parameterDef.ParameterDataTypeCode) + " };"); /* SqlDbType.Int */
					buildText.AppendLine("\t\t\t\t\tcommand.Parameters.Add(outParameter);");
				}
				else {
					buildText.Append("\t\t\t\t\tcommand.Parameters.Add(new SqlParameter(\"" + parameterDef.ParameterName + "\", " + argumentDef.ArgumentName);
					/* TODO: Find out whether we need to turn nulls into DBNull.Value, I can't recall */
	//				if (argumentDef.IsNullable == true) buildText.Append(" ?? DBNull.Value");
					buildText.AppendLine("));");
				}
			}
		}


		public void AddBuildList(StringBuilder buildText, ModelDef modelDef) {
			buildText.AppendLine("\t\t\tprivate static List<Models." + modelDef.ModelName + "> BuildList(SqlCommand command) {");
			buildText.AppendLine("\t\t\t\tvar list = new List<Models." + modelDef.ModelName + "> ();");
			buildText.AppendLine("\t\t\t\tusing (var reader = command.ExecuteReader()) {");
			buildText.AppendLine("\t\t\t\t\twhile (reader.Read()) {");
			buildText.AppendLine("\t\t\t\t\t\tvar item = MakeObject(reader, null);");
			buildText.AppendLine("\t\t\t\t\t\tlist.Add(item);");
			buildText.AppendLine("\t\t\t\t\t}");
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t\treturn list;");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine();

		}

		public void AddMakeObject(StringBuilder buildText, ModelDef modelDef) {
			var classText = "Models." + modelDef.ModelName;
			buildText.AppendLine("\t\t\tprivate static " + classText + " MakeObject(IDataRecord row, "
					+ classText + " targetToFill) {");
			buildText.AppendLine("\t\t\t\t" + classText + " outputObject = null;");
			buildText.AppendLine("\t\t\t\tif (targetToFill != null) {");
			buildText.AppendLine("\t\t\t\t\toutputObject = targetToFill;");
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t\telse {");
			buildText.AppendLine("\t\t\t\t\toutputObject = new " + classText + "();");
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("");
			buildText.AppendLine("\t\t\t\tfor (var i = 0; i < row.FieldCount; i++) {");
			buildText.AppendLine("\t\t\t\t\tFillField(outputObject, row.GetName(i), row.GetValue(i));");
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("");
			buildText.AppendLine("\t\t\t\treturn outputObject;");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine();

			buildText.AppendLine("\t\t\tprivate static void FillField(" + classText + " outputObject, string columnName, object value) {");
			buildText.AppendLine("\t\t\t\tswitch (columnName) {");

			foreach (FieldDef fieldDef in modelDef.FieldDefMap.Values) {
				PropertyDef propertyDef = fieldDef.PropertyDef;

				buildText.AppendLine("\t\t\t\t\tcase \"" + fieldDef.FieldName + "\":");
				
				if (fieldDef.BaseTableName != "" && fieldDef.BaseTableName != modelDef.ModelName) {
					
					/* e.g. Book table has a field for AuthorName, which would be in the Author table */
					buildText.AppendLine("\t\t\t\t\t\tif (outputObject." + propertyDef.PropertyName + " == null) {");
					buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = new Models." + fieldDef.BaseTableName + "();");
					buildText.AppendLine("\t\t\t\t\t\t}");
					buildText.AppendLine("\t\t\t\t\t\tDataAccess." + fieldDef.BaseTableName + ".FillField(outputObject." + propertyDef.PropertyName + ", " + fieldDef.FieldName + ", value);");

				}
				else {
					if (propertyDef.IsReference == true) {
						buildText.AppendLine("\t\t\t\t\t\tif (value == DBNull.Value) {");
						buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = null;");
						buildText.AppendLine("\t\t\t\t\t\t}");
						buildText.AppendLine("\t\t\t\t\t\telse {");
						buildText.AppendLine("\t\t\t\t\t\t\tif (outputObject." + propertyDef.PropertyName + " == null) {");
						buildText.AppendLine("\t\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = new Models." + propertyDef.PropertyTypeCode + "();");
			            buildText.AppendLine("\t\t\t\t\t\t\t}");
			            buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + "." + propertyDef.PropertyTypeCode + "ID = (" + fieldDef.DataTypeCode + ")value ;");
						buildText.AppendLine("\t\t\t\t\t\t}");
					}
					else {
						buildText.Append("\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = ");
						if (fieldDef.IsNullable) {
							buildText.Append("(value == DBNull.Value) ? null : ");
						}
						buildText.AppendLine("(" + propertyDef.PropertyTypeCode + ")value;");
					}
				}
				buildText.AppendLine("\t\t\t\t\t\tbreak;");
			}
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine("");
		}

		
		public void AddExtraResultFill(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef) {
			buildText.AppendLine("\t\t\t\t\t\t\tvar item = new " + functionDef.FunctionName + "Result {");
			buildText.Append("\t\t\t\t\t\t\t\t\t" + modelDef.ModelName + " = MakeObject(reader, null)");
			foreach (var resultPropertyDef in functionDef.ResultPropertyDefList) {
				var fieldDef = resultPropertyDef.FieldDef;
				buildText.AppendLine(",");
				buildText.Append("\t\t\t\t\t\t\t\t\t" + resultPropertyDef.PropertyName + " = ");
				if (fieldDef.IsNullable) {
					buildText.Append("(value == DBNull.Value) ? null : ");
				}
				buildText.Append("(" + resultPropertyDef.PropertyTypeCode + ")reader[\"" + fieldDef.FieldName + "\"]};");
			}
			buildText.AppendLine();
		}
		
		private string GetMicrosoftSQLDbTypeCode (string inputCode) {
			switch (inputCode) {
				case "bigint":
					return "Int64";
				case "int":
					return "Int32";
				default:
					throw new ApplicationException("Unknown input code for GetSqlDbTypeCode: " + inputCode);
			}

		}
	}

}


