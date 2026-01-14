using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuth.Db
{
	public static class Install
	{
		public static int InstallTables(CSharpAuth.Shim.PDO PDO)
		{
			//PDO.beginTransaction();
			var statement = PDO.prepare(CSharpAuthIM_CS_Auth.Properties.Resources.InstallationSql);
			int res = statement.executeNonQuery(new Dictionary<string, object>());
			//PDO.commit();
			return res;
		}
	}
}
