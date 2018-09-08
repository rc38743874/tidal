using System;
using System.Collections.Generic;
using System.Linq;

namespace TidalCSharp {
	public static class NameMapping {
		public static string GetFunctionalName(TableDef tableDef) {
			var prefix = (tableDef.SchemaName == "dbo") ? "" : tableDef.SchemaName;
			return prefix + tableDef.CleanName;
		}

		public static string MakeCleanColumnName(List<TableMapping> tableMappingList, string tableName, string columnName, bool cleanOracle) {
			if (tableMappingList != null) {
				var tableMapping = tableMappingList.FirstOrDefault(x => x.TableName == tableName);
				if (tableMapping != null) {
					if (tableMapping.ColumnArray != null) {
						var mapping = tableMapping.ColumnArray.FirstOrDefault(x => x.ColumnName == columnName);
						if (mapping != null) {
							if (mapping.PropertyName != null) {
								return mapping.PropertyName;
							}
						}
					}
				}
			}
			if (cleanOracle == true) {
				return DeOracle.Clean(columnName);
			}
			return columnName;
		}


		public static string MakeCleanTableName(List<TableMapping> tableMappingList, string tableName, bool cleanOracle) {
			if (tableMappingList != null) {
				var mapping = tableMappingList.FirstOrDefault(x => x.TableName == tableName);
				if (mapping != null) {
					if (mapping.ObjectName != null) {
						return mapping.ObjectName;
					}
				}
			}
			if (cleanOracle == true) {
				return DeOracle.Clean(tableName);
			}
			return tableName;
		}


	}
}
