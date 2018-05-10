using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

namespace TidalCSharp {
	public class ModelCreator {

		public static void MakeModels (List<TableDef> tableDefList, string namespaceText, string directory) {
			if (directory.EndsWith(Path.DirectorySeparatorChar.ToString(), false, CultureInfo.InvariantCulture) == false) {
				directory += Path.DirectorySeparatorChar;
			}
			foreach (var tableDef in tableDefList) {

                Console.WriteLine("Making model from table " + tableDef.TableName);

				StringBuilder buildText = new StringBuilder();

				buildText.AppendLine("namespace " + namespaceText + " {\n");
				buildText.AppendLine("\tpublic class " + tableDef.TableName + " {\n");
				buildText.AppendLine();

				bool needsSystem = false;

				foreach (var columnDef in tableDef.ColumnDefMap.Values) {

					string columnTypeCode;
					string columnName = columnDef.ColumnName;
					if (columnDef.ReferencedTableDef != null) {
                        if (columnName.EndsWith("Key", StringComparison.InvariantCultureIgnoreCase)) {
							columnName = columnName.Substring(0, columnName.Length - 3);
						}
						columnTypeCode = columnDef.ReferencedTableDef.TableName;
					}
					else {
						columnTypeCode = TypeConvertor.ConvertSQLToCSharp(columnDef.ColumnType);
						if (columnTypeCode == "DateTime") needsSystem = true;
						if (columnDef.IsNullable && columnTypeCode!="string") columnTypeCode += "?";
					}

					buildText.AppendLine("\t\tpublic " + columnTypeCode + " " + columnName + " { get; set; }");
				}

				buildText.AppendLine("\t}");	
				buildText.Append("}");

				if (needsSystem) {
					buildText.Insert(0, "using System;" + Environment.NewLine);
				}

				File.WriteAllText(directory + tableDef.TableName + ".cs", buildText.ToString());



			}	

			/* TODO: consider whether we want to make collections when our tables are referenced */

		}

	}
}