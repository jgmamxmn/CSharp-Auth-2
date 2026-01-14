using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuth.Auth
{
	public class DatabaseResultRow : Dictionary<string,object>
	{

	}

	public class UserDataRow
	{
		/*public int id;
		public int force_logout;
		public bool verified;
		public string email, username, password;
		public Status status;
		public Roles roles_mask;
		public bool resettable;*/

		public int id;
		public string email;
		public string password;
		public string username;
		public Status status;
		public bool verified;
		public bool resettable;
		public Roles roles_mask;
		public int registered;
		public int last_login;
		public int force_logout;
		public string internal_notes;

		private static string objToStr(DatabaseResultRow row, string key)
		{
			if (row.TryGetValue(key, out object o))
				return Shim.MasterCaster.GetString(o);
			return null;
		}
		private static int objToInt(DatabaseResultRow row, string key)
		{
			if (row.TryGetValue(key, out object o))
				return Shim.MasterCaster.GetInt(o);
			return 0;
		}
		public UserDataRow(DatabaseResultRow from)
		{
			id = objToInt(from,"id");
			email = objToStr(from, "email");
			username = objToStr(from, "username");
			password = objToStr(from, "password");
			status = (Status)objToInt(from, "status");
			force_logout = objToInt(from, "force_logout");
			verified = objToInt(from, "verified")!=0;
			roles_mask = (Roles)objToInt(from, "roles_mask");
			resettable = objToInt(from,"resettable")!=0;
			registered=objToInt(from,"registered");
			last_login=objToInt(from,"last_login");
			internal_notes=objToStr(from,"internal_notes");
		}
	}
}
