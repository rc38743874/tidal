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

		public string GetAllText(string projectNamespace, List<ModelDef> modelDefList) {
			StringBuilder buildText = new StringBuilder();
			buildText.AppendLine("using System;");
			buildText.AppendLine("using System.Collections.Generic;");
			buildText.AppendLine("using System.Data;");
			buildText.AppendLine("using System.Linq;");
			buildText.AppendLine("using System.Web;");


			var namespaceTagMap = GetNamespaceTagMap(modelDefList);

			/* get database specific lines */
			this.OutputSpecificUsingLines(buildText);
			this.OutputUsingModelsNamespace(buildText, projectNamespace, namespaceTagMap);

			// public interface IDataAccess {
			var interfaceText = new StringBuilder();
			var objectText = new StringBuilder();
			var internalInterfaceText = new StringBuilder();
			var helperText = new StringBuilder();
			var classText = new StringBuilder();

			this.ComputeTextSections(namespaceTagMap, modelDefList, interfaceText, objectText, internalInterfaceText, helperText, classText);

			buildText.AppendLine("\tpublic interface IDataAccess {");
			buildText.Append(interfaceText);
			buildText.AppendLine("\t}");
			buildText.AppendLine();

			buildText.AppendLine("\tpublic class DataAccess : IDataAccess {");
			buildText.AppendLine("\t\t#region Object Redirections");
			buildText.Append(objectText);
			buildText.AppendLine("\t\t#endregion");
			buildText.AppendLine();
			buildText.AppendLine("\t\t#region DataAccess Interfaces");
			buildText.Append(internalInterfaceText);
			buildText.AppendLine("\t\t#endregion");
			buildText.AppendLine();
			if (helperText.Length > 0) {
				buildText.AppendLine("\t\t#region Helper Functions");
				buildText.Append(helperText);
				buildText.AppendLine("\t\t#endregion");
			}

			buildText.AppendLine();
			buildText.Append(classText);
			buildText.AppendLine("\t}"); // DataAccess class
			buildText.AppendLine("}"); // namespace

			return buildText.ToString();

		}

		private Dictionary<string, string> GetNamespaceTagMap(List<ModelDef> modelDefList) {
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
			return namespaceTagMap;
		}

		public void OutputUsingModelsNamespace(StringBuilder buildText, string projectNamespace, Dictionary<string, string> namespaceTagMap) {
					/* build model namespace map */

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

		}

		public void ComputeTextSections(Dictionary<string, string> namespaceTagMap, 
					List<ModelDef> modelDefList,
					StringBuilder interfaceText, 
					StringBuilder objectText,
					StringBuilder internalInterfaceText,
					StringBuilder helperText,
					StringBuilder classText) {

			bool useGetCommand = false;
			bool useUnNull = false;
			bool useUnNullString = false;

			foreach (ModelDef modelDef in modelDefList.Where(x => x.FunctionDefList.Count > 0)) {
				interfaceText.AppendLine($"\t\tDataAccess.I{modelDef.ModelName} {modelDef.ModelName} {{ get; }}");
				interfaceText.AppendLine();

				objectText.AppendLine($"\t\tprivate I{modelDef.ModelName} _{modelDef.ModelName};");
				objectText.AppendLine();
				objectText.AppendLine($"\t\tI{modelDef.ModelName} IDataAccess.{modelDef.ModelName} {{");
				objectText.AppendLine($"\t\t\tget {{");
				objectText.AppendLine($"\t\t\t\tif (this._{modelDef.ModelName} == null) this._{modelDef.ModelName} = new DataAccess.{modelDef.ModelName}();");
				objectText.AppendLine($"\t\t\t\treturn this._{modelDef.ModelName};");
				objectText.AppendLine("\t\t\t}");
				objectText.AppendLine("\t\t}");
				objectText.AppendLine();

				internalInterfaceText.AppendLine($"\t\tpublic interface I{modelDef.ModelName} {{");
				internalInterfaceText.AppendLine();
            // List<Models.ClassificationLevel> ListForActiveFlag(SqlConnection conn, bool activeFlag);


				classText.AppendLine($"\t\tprivate class {modelDef.ModelName} : I{modelDef.ModelName} {{");
				classText.AppendLine("");

				foreach (FunctionDef functionDef in modelDef.FunctionDefList) {
					if (functionDef.UsesResult == true) {
						classText.AppendLine("\t\t\tpublic class " + functionDef.FunctionName + "Result {");
						if (modelDef.IsJustTable == false) {
							classText.Append("\t\t\t\tpublic ");
							if (functionDef.ReturnTypeNamespace != null) classText.Append(namespaceTagMap[functionDef.ReturnTypeNamespace] + ".");
							classText.AppendLine(functionDef.ReturnTypeCode + " " + modelDef.ModelName + " {get; set;}");
						}

						foreach (ResultPropertyDef rpDef in functionDef.ResultPropertyDefList) {
							classText.AppendLine("\t\t\t\tpublic " + OutputNullableType(rpDef.PropertyTypeCode, rpDef.FieldDef.IsNullable) + " " + rpDef.PropertyName + " {get; set;}");
						}
						classText.AppendLine("\t\t\t}");
					}
				}


				/* make extra class if the field is not part of the model for the table, and it's not a Key-suffixed reference */
				/* in that case we make a public class with the table name + function name + the word Result, for example AuthorCreateNameResult
					that contains all of the fields that are not part of the model.   */



				foreach (FunctionDef functionDef in modelDef.FunctionDefList) {
					if (functionDef.OutputsList == true) {
						CreateFunctionMultiRow(classText, internalInterfaceText, modelDef, functionDef, namespaceTagMap);
					}
					else if (functionDef.OutputsObject == true) {
						/* TODO: if it's only a single field output, should it return just that as a return value? */
						CreateFunctionSingleRow(classText, internalInterfaceText, modelDef, functionDef, namespaceTagMap);
					}
					else {
						CreateFunctionNoRows(classText, internalInterfaceText, modelDef, functionDef, namespaceTagMap);
					}
					classText.AppendLine("");
				}

				if (modelDef.IsJustTable == false) {
					if (modelDef.UsesBuildListFunction == true) {
						AddBuildList(classText, modelDef, namespaceTagMap);
					}

					if (modelDef.UsesMakeObjectFunction == true) {
						AddMakeObject(classText, modelDef, namespaceTagMap);
					}
				}

				classText.AppendLine("\t\t}");
				classText.AppendLine("");

				internalInterfaceText.AppendLine("\t\t}");
				internalInterfaceText.AppendLine();

			}

			/* TODO: only activate if we use them */
			useGetCommand = true;
			useUnNull = true;
			useUnNullString = true;


			if (useGetCommand == true) {
				helperText.AppendLine("\t\tpublic static " + this.GetSqlCommandText() + " GetCommand(" + this.GetSqlConnectionText() + " conn, " + this.GetSqlTransactionText() + " trans, string procedureName) {");
				helperText.AppendLine("\t\t\treturn new " + this.GetSqlCommandText() + "(procedureName, conn, trans) { CommandType = CommandType.StoredProcedure };");
				helperText.AppendLine("\t\t}");
				helperText.AppendLine("");
			}
			if (useUnNull == true) {
				helperText.AppendLine("\t\tpublic static Nullable<T> UnNull<T>(object value) where T : struct {");
				helperText.AppendLine("\t\t\tif (value == DBNull.Value) return null;");
				helperText.AppendLine("\t\t\treturn (T)value;");
				helperText.AppendLine("\t\t}");
				helperText.AppendLine("");
			}

			if (useUnNullString == true) {
				helperText.AppendLine("\t\tpublic static string UnNullString(object value) {");
				helperText.AppendLine("\t\t\tif (value == DBNull.Value) return null;");
				helperText.AppendLine("\t\t\treturn (string)value;");
				helperText.AppendLine("\t\t}");
				helperText.AppendLine("");
			}

		}


		public void CreateFunctionNoRows(StringBuilder buildText, StringBuilder internalInterfaceText, ModelDef modelDef, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {
			/* version using all arguments */

			string functionString = GetFunctionSignature(functionDef, namespaceTagMap);
			string argumentsString = GetArguments(functionDef);
			string functionSignature = functionString + argumentsString + ")";

			internalInterfaceText.AppendLine("\t\t\t" + functionSignature + ";");
			internalInterfaceText.AppendLine();

			buildText.Append("\t\t\tpublic " + functionSignature + " {");
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
			if (functionDef.ArgumentDefList.Any(x => x.PropertyDef != null)) {

				string functionSignatureWithObject = functionString + GetObjectArgument(modelDef, namespaceTagMap)
					    + GetArgumentsNotInObject(functionDef);

				internalInterfaceText.AppendLine("\t\t\t" + functionSignatureWithObject + ";");
				internalInterfaceText.AppendLine();
			
				buildText.AppendLine("\t\t\tpublic " + functionSignatureWithObject + " {");
				buildText.Append("\t\t\t\t");
				if (functionDef.ReturnTypeCode != null) {
					buildText.Append("return ");
				}
				buildText.AppendLine(modelDef.ModelName + "." + functionDef.FunctionName + "(conn, trans,");
				AddFunctionArguments(buildText, modelDef, functionDef);

				buildText.AppendLine("\t\t\t}");
			}

		}




					


		public void CreateFunctionMultiRow(StringBuilder buildText, StringBuilder internalInterfaceText, ModelDef modelDef, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {

			string functionString = GetFunctionSignature(functionDef, namespaceTagMap);
			string argumentsString = GetArguments(functionDef);
			string functionSignature = functionString + argumentsString + ")";

			internalInterfaceText.AppendLine("\t\t\t" + functionSignature + ";");
			internalInterfaceText.AppendLine();

			buildText.AppendLine("\t\t\tpublic " + functionSignature + " {");
			AddUsingCommand(buildText, functionDef.ProcedureDef);
			AddCommandParameters(buildText, functionDef);

			if (functionDef.UsesResult == true) {
				buildText.AppendLine("\t\t\t\t\tvar list = new List<" + functionDef.FunctionName + "Result>();");
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


		public void CreateFunctionSingleRow(StringBuilder buildText, StringBuilder internalInterfaceText, ModelDef modelDef, FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {

			string functionString = GetFunctionSignature(functionDef, namespaceTagMap);
			string argumentsString = GetArguments(functionDef);
			string functionSignature = functionString + argumentsString + ")";
			internalInterfaceText.AppendLine("\t\t\t" + functionSignature + ";");
			internalInterfaceText.AppendLine();

			buildText.AppendLine("\t\t\tpublic " + functionSignature + " {");
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

			/* Only use the Object input version if there are fields that come from the object */
			if (functionDef.ArgumentDefList.Any(x => x.PropertyDef != null)) {

//				string functionSignatureWithObject = functionString + GetObjectArgument(modelDef, namespaceTagMap)
//					    + GetArgumentsNotInObject(functionDef);

				string functionSignatureWithObject = functionString + GetObjectArgument(modelDef, namespaceTagMap) + ")";

				internalInterfaceText.AppendLine("\t\t\t" + functionSignatureWithObject + ";");
				internalInterfaceText.AppendLine();

				buildText.Append("\t\t\tpublic " + functionSignatureWithObject + " {");
				buildText.AppendLine("\t\t\t\treturn " + modelDef.ModelName + "." + functionDef.FunctionName + "(conn, trans,");
				AddFunctionArguments(buildText, modelDef, functionDef);
				buildText.AppendLine("\t\t\t}");
			}
		}


		public string GetFunctionSignature(FunctionDef functionDef, Dictionary<string, string> namespaceTagMap) {
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
			return code + " " + functionDef.FunctionName + "(SqlConnection conn,\n\t\t\t\t\t\tSqlTransaction trans";
		}

		public string GetArguments(FunctionDef functionDef) {
			StringBuilder buildText = new StringBuilder();
			foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				buildText.AppendLine(",");
				buildText.Append("\t\t\t\t\t\t" + OutputNullableType(argumentDef.ArgumentTypeCode, argumentDef.IsNullable) + " " + argumentDef.ArgumentName);
			}
			return buildText.ToString();
		}

		public string GetObjectArgument(ModelDef modelDef, Dictionary<string, string> namespaceTagMap) {
			StringBuilder buildText = new StringBuilder();
			buildText.AppendLine(",");
			buildText.Append("\t\t\t\t\t\t");
			buildText.AppendLine(namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName + " inputObject");
			buildText.AppendLine();
			return buildText.ToString();
		}

		public string GetArgumentsNotInObject(FunctionDef functionDef) {
			StringBuilder buildText = new StringBuilder();
			foreach (ArgumentDef argumentDef in functionDef.ArgumentDefList) {
				if (argumentDef.PropertyDef == null) {
					buildText.AppendLine(",");
					buildText.Append("\t\t\t\t\t\t" + argumentDef.ArgumentTypeCode + (argumentDef.IsNullable ? "?" : "") + " " + argumentDef.ArgumentName);
				}
			}
			return buildText.ToString();
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
						Console.WriteLine("Warning: Argument " + argumentDef.ArgumentName + " in function " + functionDef.FunctionName + " for " + functionDef.ProcedureDef.ProcedureName + " had a null PropertyDef.  Should the column exist in the model class but is missing?  Perhaps it is spelled differently and needs an entry in the mapping file referenced by --namemapfile?");
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
					/* TODO: arguments are not getting set to Nullable:true, but this would probably work for everything no? */
					buildText.Append("(object) " + argumentDef.ArgumentName + " ?? DBNull.Value");
					//if (argumentDef.IsNullable == true) {
					//	buildText.Append("(object) " + argumentDef.ArgumentName + " ?? DBNull.Value");
					//}
					//else {
					//  buildText.Append(argumentDef.ArgumentName);
					//}
					buildText.AppendLine("));");
				}
			}
		}
		// command.Parameters.Add(new SqlParameter("@OfficialPositionCode", officialPositionCode ?? DBNull.Value));
					

		public void AddBuildList(StringBuilder buildText, ModelDef modelDef, Dictionary<string, string> namespaceTagMap) {
			buildText.AppendLine("\t\t\tprivate List<" + namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName + "> BuildList(" + this.GetSqlCommandText() + " command) {");
			buildText.AppendLine("\t\t\t\tvar list = new List<" + namespaceTagMap[modelDef.Namespace] + "." + modelDef.ModelName + ">();");
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
			buildText.AppendLine("\t\t\tprivate " + classText + " MakeObject(IDataRecord row, "
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

			buildText.AppendLine("\t\t\tprivate void FillField(" + classText + " outputObject, string columnName, object value) {");
			buildText.AppendLine("\t\t\t\tswitch (columnName) {");

			foreach (FieldDef fieldDef in modelDef.FieldDefMap.Values.OrderBy(x => x.FieldName)) {
				PropertyDef propertyDef = fieldDef.PropertyDef;

				buildText.AppendLine("\t\t\t\t\tcase \"" + fieldDef.FieldName + "\":");

				var fieldTableName = fieldDef.ProcedureDef.TableDef.TableName;

				if (fieldDef.PropertyDefChain != null) {
					/* TODO: Not exactly sure yet how to handle a NULL value for a secondary model's field */

					var bigName = "outputObject";
					string lastModelName = null;
					foreach (PropertyDef subDef in fieldDef.PropertyDefChain) {
						if (subDef != fieldDef.PropertyDefChain.Last()) {
							buildText.AppendLine($"\t\t\t\t\t\tif ({bigName}.{subDef.PropertyName} == null) {{");
							buildText.AppendLine($"\t\t\t\t\t\t\t{bigName}.{subDef.PropertyName} = new {namespaceTagMap[subDef.PropertyTypeNamespace]}.{subDef.PropertyTypeCode}();");
							buildText.AppendLine("\t\t\t\t\t\t}");
							bigName += "." + subDef.PropertyName;
							lastModelName = subDef.PropertyTypeCode;
						} else {
							/* TODO: not sure this will work with schemas, have to try it */
							/* TODO: don't use the DataAccess.<Model>.FillField function, because it will not exist unless 
							 * we've referenced that model directly in a function.  Instead just populate the field immediately
							 * like we do any other field. */
							// buildText.AppendLine("\t\t\t\t\t\tDataAccess." + lastModelName + ".FillField(" + bigName + ", \"" + fieldDef.FieldName + "\", value);");
							buildText.Append($"\t\t\t\t\t\t{bigName}.{subDef.PropertyName} = ");
							AddFillFieldColumnSet(buildText, fieldDef, subDef, namespaceTagMap);
						}
					}


				} else {
					if (propertyDef != null && fieldDef.BaseTableName != "" && fieldDef.BaseTableName != fieldTableName) {
						/* TODO: I *think* we can remove this in favor of using a property def chain above */
						/* e.g. Book table has a field for AuthorName, which would be in the Author table */
						buildText.AppendLine("\t\t\t\t\t\tif (outputObject." + propertyDef.PropertyName + " == null) {");

						//					Console.WriteLine($"DEBUG:fieldDef.FieldName={fieldDef.FieldName}, modelDef.ModelName={modelDef.ModelName}, propertyDef.PropertyName={propertyDef.PropertyName}, propertyDef.PropertyTypeNamespace={propertyDef.PropertyTypeNamespace}, fieldDef.BaseTableName={fieldDef.BaseTableName}");
						buildText.AppendLine("\t\t\t\t\t\t\toutputObject." + propertyDef.PropertyName + " = new " + namespaceTagMap[propertyDef.PropertyTypeNamespace] + "." + fieldDef.BaseTableName + "();");
						buildText.AppendLine("\t\t\t\t\t\t}");
						buildText.AppendLine("\t\t\t\t\t\tDataAccess." + fieldDef.BaseTableName + ".FillField(outputObject." + propertyDef.PropertyName + ", \"" + fieldDef.FieldName + "\", value);");

					} else {
						if (propertyDef == null) {
							/* TODO: should this be empty, or should it maybe use a *Result class? */
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
								AddFillFieldColumnSet(buildText, fieldDef, propertyDef, namespaceTagMap);
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
		}

		public void AddFillFieldColumnSet(StringBuilder buildText, FieldDef fieldDef, PropertyDef propertyDef, Dictionary<string, string> namespaceTagMap) {
			if (propertyDef.IsEnum == true) {
				string enumType = namespaceTagMap[propertyDef.PropertyTypeNamespace] + "." + propertyDef.PropertyTypeCode;
				buildText.AppendLine("(" + enumType + ")Enum.Parse(typeof(" + enumType + "), (string)value);");
			} else {
				// if (fieldDef.IsNullable && propertyDef.PropertyTypeCode != "string") {
				if (fieldDef.IsNullable) {
					if (propertyDef.PropertyTypeCode == "string") {
						buildText.Append("UnNullString(value)");
					} else {
						OutputUnNull(buildText, propertyDef.PropertyTypeCode, "value");
					}
					buildText.AppendLine(";");
				} else {
					buildText.AppendLine("(" + propertyDef.PropertyTypeCode + ")value;");
				}
			}
		}


		public void AddExtraResultFill(StringBuilder buildText, ModelDef modelDef, FunctionDef functionDef) {
			buildText.AppendLine("\t\t\t\t\t\t\tvar item = new " + functionDef.FunctionName + "Result {");
			bool first = true;
			if (modelDef.IsJustTable == false) {
				buildText.Append("\t\t\t\t\t\t\t\t\t" + modelDef.ModelName + " = MakeObject(reader, null)");
				first = false;
			}
			foreach (var resultPropertyDef in functionDef.ResultPropertyDefList) {
				if (first == true) {
					first = false;
				} 
				else {
					buildText.AppendLine(",");
				}


				var fieldDef = resultPropertyDef.FieldDef;

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


