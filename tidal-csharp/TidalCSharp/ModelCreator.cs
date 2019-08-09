using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TidalCSharp {
	public class ModelCreator {

		public static void MakeModels (List<TableDef> tableDefList, string namespaceText, string directory, List<TableMapping> tableMappingList, bool cleanOracle) {
			if (directory.EndsWith(Path.DirectorySeparatorChar.ToString(), false, CultureInfo.InvariantCulture) == false) {
				directory += Path.DirectorySeparatorChar;
			}
			foreach (var tableDef in tableDefList.OrderBy(x => x.TableName)) {
				/* TODO: For now we skip views, but should we? */
				if (tableDef.TableType == "TABLE") {
					Shared.Info("Making model from table " + tableDef.TableName);

					var modelName = NameMapping.MakeCleanTableName(tableMappingList, tableDef.TableName, cleanOracle);
					Shared.Info($"\t\tusing model name {modelName}");

					StringBuilder buildText = new StringBuilder();

					buildText.AppendLine("namespace " + namespaceText + " {\n");
					buildText.AppendLine("\tpublic partial class " + modelName + " {\n");
					buildText.AppendLine();

					bool needsSystem = false;

					foreach (var columnDef in tableDef.ColumnDefMap.Values) {

						string columnTypeCode;
						string columnName = columnDef.ColumnName;

						string convertedColumnName = NameMapping.MakeCleanColumnName(tableMappingList, tableDef.TableName, modelName, columnName, cleanOracle);


						if (columnDef.ReferencedTableDef != null) {
							if (convertedColumnName.EndsWith("Key", StringComparison.InvariantCultureIgnoreCase)) {
								convertedColumnName = convertedColumnName.Substring(0, convertedColumnName.Length - 3);
							}
							var rawReferencedTableName = columnDef.ReferencedTableDef.TableName;
							columnTypeCode = NameMapping.MakeCleanTableName(tableMappingList, rawReferencedTableName, cleanOracle);
						} else {
							columnTypeCode = TypeConvertor.ConvertSQLToCSharp(columnDef.ColumnType);
							if (columnTypeCode == "DateTime") needsSystem = true;
							if (columnTypeCode.Contains("[]")) {
								needsSystem = true;
							}
							else {
								if (columnDef.ForceToBit == true) {
									columnTypeCode = "bool";
								}
								if (columnDef.IsNullable && columnTypeCode != "string") columnTypeCode += "?";
							}
						}



						buildText.AppendLine("\t\tpublic " + columnTypeCode + " " + convertedColumnName + " { get; set; }");
					}

					buildText.AppendLine("\t}");
					buildText.Append("}");

					if (needsSystem) {
						buildText.Insert(0, "using System;" + Environment.NewLine);
					}

					File.WriteAllText(directory + modelName + ".cs", buildText.ToString());
				}

			}	

			/* TODO: consider whether we want to make collections when our tables are referenced */

		}

	}
}