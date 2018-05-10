using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TidalCSharp {
	public abstract class ClassCreatorBase {

		protected abstract void OutputSpecificUsingLines(StringBuilder buildText);
		protected abstract string GetSqlCommandText();
		protected abstract string GetSqlConnectionText();
		protected abstract string GetSqlTransactionText();
		protected abstract string GetSqlParameterText();
		protected abstract string GetSqlDbTypeText();
		protected abstract string GetSqlDbTypeCode(string inputCode);

		public string GetDataAccessClassText(string projectNamespace, List<ModelDef> modelDefList) {
			StringBuilder buildText = new StringBuilder();
			buildText.AppendLine("using System;");
			buildText.AppendLine("using System.Collections.Generic;");
			buildText.AppendLine("using System.Data;");
			buildText.AppendLine("using System.Linq;");
			buildText.AppendLine("using System.Web;");
			this.OutputSpecificUsingLines(buildText);


			/* build model namespace map */
			List<string> modelNamespaceList = modelDefList
					.Where(x => x.FunctionDefList.Count > 0)
					.Select(x => x.Namespace)
					.Distinct()
					.OrderBy(x => x)
					.ToList();

			var unmappedNamespaceList = modelNamespaceList.Where(x => x.Contains(".") == false).ToList();

			Dictionary<string, string> namespaceTagMap = unmappedNamespaceList.ToDictionary(x => x, x => x);

			/* in case someone actually used Models or Models1 etc. as a namespace check to skip that tag */
			int nsIndex = 0;
			foreach (string namespaceName in modelNamespaceList) {
				if (namespaceTagMap.ContainsKey(namespaceName) == false) {
					string suffix;
					do {
						suffix = (nsIndex == 0) ? "" : nsIndex.ToString();
						nsIndex++;
					} while (unmappedNamespaceList.Contains("Models" + suffix));
					namespaceTagMap[namespaceName] = "Models" + suffix;
				}
			}
			var reverseMap = namespaceTagMap.ToDictionary(x => x.Value, x => x.Key);
			foreach (var key in namespaceTagMap.Values.OrderBy(x => x)) {
				if (key != reverseMap[key]) {
					buildText.AppendLine("using " + key + " = " + reverseMap[key] + ";");
				}
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
			buildText.AppendLine("\t\tpublic static " + this.GetSqlCommandText() + " GetCommand(" + this.GetSqlConnectionText() + " conn, " + this.GetSqlTransactionText() + " trans, string procedureName) {");
			buildText.AppendLine("\t\t\treturn new " + this.GetSqlCommandText() + "(procedureName, conn, trans) { CommandType = CommandType.StoredProcedure };");
			buildText.AppendLine("\t\t}");
			buildText.AppendLine("");
			buildText.AppendLine("\t\tpublic static Nullable<T> UnNull<T>(object value) where T : struct {");
			buildText.AppendLine("\t\t\tif (value == DBNull.Value) return null;");
			buildText.AppendLine("\t\t\treturn (T)value;");
			buildText.AppendLine("\t\t}");
			buildText.AppendLine("");


			foreach (ModelDef modelDef in modelDefList.Where(x => x.FunctionDefList.Count > 0)) {

				buildText.AppendLine("\t\tpublic class " + modelDef.ModelName + " {");
				buildText.AppendLine("");

				foreach (FunctionDef functionDef in modelDef.FunctionDefList) {
					if (functionDef.UsesResult == true) {
						buildText.AppendLine("\t\t\tpublic class " + functionDef.FunctionName + "Result {");
						buildText.Append("\t\t\t\tpublic ");
						if (functionDef.ReturnTypeNamespace != null) buildText.Append(namespaceTagMap[functionDef.ReturnTypeNamespace] + ".");
						buildText.AppendLine(functionDef.ReturnTypeCode + " " + modelDef.ModelName + " {get; set;}");

						foreach (ResultPropertyDef rpDef in functionDef.ResultPropertyDefList) {
							buildText.AppendLine("\t\t\t\tpublic " + OutputNullableType(rpDef.PropertyTypeCode, rpDef.FieldDef.IsNullable) + " " + rpDef.PropertyName + " {get; set;}");
						}
						buildText.AppendLine("\t\t\t}");
					}
				}


				/* make extra class if the field is not part of the model for the table, and it's not a Key-suffixed reference */
				/* in that case we make a public static class with the table name + function name + the word Result, for example AuthorCreateNameResult
					that contains all of the fields that are not part of the model.   */



				foreach (FunctionDef functionDef in modelDef.FunctionDefList) {
					if (functionDef.OutputsList == true) {
						CreateFunctionMultiRow(buildText, modelDef, functionDef, namespaceTagMap);
					}
					else if (functionDef.OutputsObject == true) {
						/* TODO: if it's only a single field output, should it return just that as a return value? */
						CreateFunctionSingleRow(buildText, modelDef, functionDef, namespaceTagMap);
					}
					else {
						CreateFunctionNoRows(buildText, modelDef, functionDef, namespaceTagMap);
					}
					buildText.AppendLine("");
				}


				if (modelDef.UsesBuildListFunction == true) {
					AddBuildList(buildText, modelDef, namespaceTagMap);
				}

				if (modelDef.UsesMakeObjectFunction == true) {
					AddMakeObject(buildText, modelDef, namespaceTagMap);
				}

				buildText.AppendLine("\t\t}");
				buildText.AppendLine("");

			}

			buildText.AppendLine("\t}"); // DataAccess
			buildText.AppendLine("}"); // namespace
			return buildText.ToString();
		}


		public void CreateFunctionNoRows(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {
			/* version using all arguments */
			AddFunctionDeclaration(buildText, functionDef, namespaceTagMap);
			AddArguments(buildText, functionDef);
			AddUsingCommand(buildText, functionDef.ProcedureDef);

			AddCommandParameters(buildText, functionDef);

			buildText.AppendLine("");
			buildText.AppendLine("\t\t\t\t\tcommand.ExecuteNonQuery();");
			if (functionDef.ReturnTypeCode != null) {
				buildText.Append("\t\t\t\t\treturn (");
				if (functionDef.ReturnTypeNamespace != null) buildText.Append(namespaceTagMap[functionDef.ReturnTypeNamespace] + ".");
				buildText.AppendLine(functionDef.ReturnTypeCode + ")outParameter.Value;");
			}
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine("");


			/* version using object as input */
			AddFunctionDeclaration(buildText, functionDef, namespaceTagMap);
			AddObjectArgument(buildText, modelDef, namespaceTagMap);
			AddArgumentsNotInObject(buildText, functionDef);
			buildText.AppendLine(") {");

			buildText.Append("\t\t\t\t");
			if (functionDef.ReturnTypeCode != null) {
				buildText.Append("return ");
			}
			buildText.AppendLine(modelDef.ModelName + "." + functionDef.FunctionName + "(conn, trans,");
			AddFunctionArguments(buildText, modelDef, functionDef);

			buildText.AppendLine("\t\t\t}");

		}





		public void CreateFunctionMultiRow(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {
			AddFunctionDeclaration(buildText, functionDef, namespaceTagMap);
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


		public void CreateFunctionSingleRow(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {
			AddFunctionDeclaration(buildText, functionDef, namespaceTagMap);
			AddArguments(buildText, functionDef);
			AddUsingCommand(buildText, functionDef.ProcedureDef);
			AddCommandParameters(buildText, functionDef);
			buildText.AppendLine("\t\t\t\t\tusing (var reader = command.ExecuteReader()) {");
			buildText.AppendLine("\t\t\t\t\t\tif (reader.Read()) {");

			if (functionDef.UsesResult) {
				AddExtraResultFill(buildText, modelDef, functionDef);
				buildText.AppendLine("\t\t\t\t\t\t\treturn item;");
			} else {
				buildText.AppendLine("\t\t\t\t\t\t\treturn MakeObject(reader, null);");
			}
			buildText.AppendLine("\t\t\t\t\t\t} else {");
			buildText.AppendLine("\t\t\t\t\t\t\treturn null;");
			buildText.AppendLine("\t\t\t\t\t\t}");
			buildText.AppendLine("\t\t\t\t\t}");
			buildText.AppendLine("\t\t\t\t}");
			buildText.AppendLine("\t\t\t}");
			buildText.AppendLine("");

			/* e.g. Read from object */
			AddFunctionDeclaration(buildText, functionDef, namespaceTagMap);
			AddObjectArgument(buildText, modelDef, namespaceTagMap);
			buildText.AppendLine(") {");
			buildText.AppendLine("\t\t\t\treturn " + modelDef.ModelName + "." + functionDef.FunctionName + "(conn, trans,");
			AddFunctionArguments(buildText, modelDef, functionDef);
			buildText.AppendLine("\t\t\t}");
		}

		public void AddFunctionDeclaration(StringBuilder buildText, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {
			string code = "";
			if (functionDef.UsesResult) {
				code = functionDef.FunctionName + "Result";
			} else {
				if (functionDef.ReturnTypeCode != null) {
					if (functionDef.ReturnTypeNamespace != null) code = namespaceTagMap[functionDef.ReturnTypeNamespace] + ".";
					code += functionDef.ReturnTypeCode;
				} else {
					code = "void";
				}
			}

			if (functionDef.OutputsList) code = "List<" + code + ">";
			buildText.Append("\t\t\tpublic static " + code + " " + functionDef.FunctionName + "(SqlConnection conn, SqlTransaction trans");
		}

		public void AddArguments(StringBuilder buildText, FunctionDef functionDef) {
			foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				buildText.AppendLine(",");
				buildText.Append("\t\t\t\t\t\t" + OutputNullableType(argumentDef.ArgumentTypeCode, argumentDef.IsNullable) + " " + argumentDef.ArgumentName);
			}
			buildText.AppendLine(") {");
			buildText.AppendLine("");

		}

		public void AddObjectArgument(StringBuilder buildText, ModelDef modelDef, Dictionary<string, string> namespaceTagMap) {
			buildText.AppendLine(",");
			buildText.Append("\t\t\t\t\t\t");
			buildText.AppendLine(namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName + " inputObject");
			buildText.AppendLine();
		}

		public void AddArgumentsNotInObject(StringBuilder buildText, FunctionDef functionDef) {
			foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				if (argumentDef.PropertyDef == null) {
					buildText.AppendLine(",");
					buildText.Append("\t\t\t\t\t\t" + argumentDef.ArgumentTypeCode + (argumentDef.IsNullable ? "?" : "") + " " + argumentDef.ArgumentName);
				}
			}
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

				if (propertyDef == null) {
					/* display a friendly warning if the field might be missing in the target class */
					if (argumentDef.ArgumentName.EndsWith("Key", false, CultureInfo.InvariantCulture)) {
						Console.WriteLine("Warning: Argument " + argumentDef.ArgumentName + " in function " + functionDef.FunctionName + " for " + functionDef.ProcedureDef.ProcedureName + " had a null PropertyDef.  Perhaps a foreign key reference doesn't exist on that column of the database table?");
					} else {
						Console.WriteLine("Warning: Argument " + argumentDef.ArgumentName + " in function " + functionDef.FunctionName + " for " + functionDef.ProcedureDef.ProcedureName + " had a null PropertyDef.  Should the column exist in the model class but is missing?");
					}
					buildText.Append(argumentDef.ArgumentName);
				} else {
					if (propertyDef.IsReference) {
						string subPropertyName = propertyDef.PropertyName;
						if (argumentDef.ArgumentName.EndsWith("Key", false, CultureInfo.InvariantCulture)) {
							/* TODO: this is failing when type is an interface */
							subPropertyName = propertyDef.PropertyTypeCode + "ID";
						}
						if (argumentDef.IsNullable) {
							buildText.Append("(inputObject." + propertyDef.PropertyName + " == null) ? (" + argumentDef.ArgumentTypeCode + "?)null : inputObject." + propertyDef.PropertyName + "." + subPropertyName);
						} else {
							buildText.Append("inputObject." + propertyDef.PropertyName + "." + subPropertyName);
						}
					} else {
						buildText.Append("inputObject." + propertyDef.PropertyName);
						if (propertyDef.IsEnum) {
							buildText.Append(".ToString()");
						}
					}
				}
			}
			buildText.AppendLine(");");
		}

		public void AddUsingCommand(StringBuilder buildText, ProcedureDef procedureDef) {
			buildText.AppendLine("\t\t\t\tusing (var command = DataAccess.GetCommand(conn, trans, \"" + procedureDef.ProcedureName + "\")) {");
		}

		public void AddCommandParameters(StringBuilder buildText, FunctionDef functionDef) {
			foreach (ParameterDef parameterDef in functionDef.ProcedureDef.ParameterDefMap.Values) {
				ArgumentDef argumentDef = parameterDef.ArgumentDef;
				if (parameterDef.IsOutParameter == true) {
					buildText.AppendLine("\t\t\t\t\tvar outParameter = new " + this.GetSqlParameterText() + " { ParameterName = \"" + parameterDef.ParameterName + "\", Direction = ParameterDirection.Output, Size = " + parameterDef.ParameterSize + ", " + this.GetSqlDbTypeText() + " = " + this.GetSqlDbTypeText() + "." + GetSqlDbTypeCode(parameterDef.ParameterDataTypeCode) + " };");
					buildText.AppendLine("\t\t\t\t\tcommand.Parameters.Add(outParameter);");
				} else {
					buildText.Append("\t\t\t\t\tcommand.Parameters.Add(new " + this.GetSqlParameterText() + "(\"" + parameterDef.ParameterName + "\", ");
					if (argumentDef.IsNullable == true) {
						buildText.Append("(object) " + argumentDef.ArgumentName + " ?? DBNull.Value");
					}
					else {
						buildText.Append(argumentDef.ArgumentName);
					}
					buildText.AppendLine("));");
				}
			}
		}


		public void AddBuildList(StringBuilder buildText, ModelDef modelDef, Dictionary<string, string> namespaceTagMap) {
			buildText.AppendLine("\t\t\tprivate static List<" + namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName + "> BuildList(" + this.GetSqlCommandText() + " command) {");
			buildText.AppendLine("\t\t\t\tvar list = new List<" + namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName + "> ();");
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

		public void AddMakeObject(StringBuilder buildText, ModelDef modelDef, Dictionary<string, string> namespaceTagMap) {
			var classText = namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName;
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
					buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = new " + namespaceTagMap[propertyDef.PropertyTypeNamespace] + "." + fieldDef.BaseTableName + "();");
					buildText.AppendLine("\t\t\t\t\t\t}");
					buildText.AppendLine("\t\t\t\t\t\tDataAccess." + fieldDef.BaseTableName + ".FillField(outputObject." + propertyDef.PropertyName + ", " + fieldDef.FieldName + ", value);");

				} else {
					if (propertyDef == null) {
						buildText.AppendLine("\t\t\t\t\t\t/* ignoring field " + fieldDef.FieldName + " */");
					} else {
						if (propertyDef.IsReference == true) {
							buildText.AppendLine("\t\t\t\t\t\tif (value == DBNull.Value) {");
							buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = null;");
							buildText.AppendLine("\t\t\t\t\t\t}");
							buildText.AppendLine("\t\t\t\t\t\telse {");
							buildText.AppendLine("\t\t\t\t\t\t\tif (outputObject." + propertyDef.PropertyName + " == null) {");
							buildText.AppendLine("\t\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = new " + namespaceTagMap[propertyDef.PropertyTypeNamespace] + "." + propertyDef.PropertyTypeCode + "();");
							buildText.AppendLine("\t\t\t\t\t\t\t}");
							buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + "." + propertyDef.PropertyTypeCode + "ID = (" + fieldDef.DataTypeCode + ")value ;");
							buildText.AppendLine("\t\t\t\t\t\t}");
						} else {
							buildText.Append("\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = ");
							if (propertyDef.IsEnum == true) {
								string enumType = namespaceTagMap[propertyDef.PropertyTypeNamespace] + "." + propertyDef.PropertyTypeCode;
								buildText.AppendLine("(" + enumType + ")Enum.Parse(typeof(" + enumType + "), (string)value);");
							} else {
								if (fieldDef.IsNullable && propertyDef.PropertyTypeCode != "string") {
									OutputUnNull(buildText, propertyDef.PropertyTypeCode, "value");
									buildText.AppendLine(";");
								} else {
									buildText.AppendLine("(" + propertyDef.PropertyTypeCode + ")value;");
								}
							}
						}
					}
				}
				buildText.AppendLine("\t\t\t\t\t\tbreak;");
			}
			buildText.AppendLine("\t\t\t\t\tdefault:");
			buildText.AppendLine("\t\t\t\t\t\tthrow new ApplicationException(\"Unrecognized column name: \" + columnName);");
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
				if (fieldDef.IsNullable && resultPropertyDef.PropertyTypeCode != "string") {
					OutputUnNull(buildText, resultPropertyDef.PropertyTypeCode, "reader[\"" + fieldDef.FieldName + "\"]");
				} else {
					buildText.Append("(" + resultPropertyDef.PropertyTypeCode + ")reader[\"" + fieldDef.FieldName + "\"]");
				}
			}
			buildText.AppendLine("};");
		}

		private void OutputUnNull(StringBuilder buildText, string propertyTypeCode, string contents) {
			if (propertyTypeCode.EndsWith("?", false, CultureInfo.InvariantCulture)) propertyTypeCode = propertyTypeCode.Substring(0, propertyTypeCode.Length - 1);
			buildText.Append("UnNull<" + propertyTypeCode + ">(" + contents + ")");
		}

		/* since strings as database fields could be nullable, but are never a nullable type, 
		 * we add this check here to write the C# type correctly */
		private string OutputNullableType(string typeName, bool isNullable) {
			if (typeName == "string") return "string";
			return typeName + (isNullable ? "?" : "");
		}

		
	}

}


