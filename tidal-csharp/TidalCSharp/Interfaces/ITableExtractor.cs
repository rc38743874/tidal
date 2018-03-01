using System;
using System.Collections.Generic;
using System.Data.Common;

namespace TidalCSharp {

	public interface ITableExtractor {
		
		TableDefMap ExtractTableData();
		
	}
}

