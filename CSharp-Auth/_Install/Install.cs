using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delight.Db
{
	public static class Install
	{
		public static int InstallTables(Delight.Shim.PDO PDO)
		{
			//PDO.beginTransaction();
			var statement = PDO.prepare(DelightIM_CS_Auth.Properties.Resources.InstallationSql);
			int res = statement.executeNonQuery(new Dictionary<string, object>());
			//PDO.commit();
			return res;
		}
	}
}
