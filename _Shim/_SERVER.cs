using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delight.Shim
{
	public class _SERVER
	{
		public _SERVER(in string RemoteIp)
		{
			REMOTE_ADDR = RemoteIp;
		}

		//virtual public string REMOTE_ADDR => "127.0.0.1";
		public readonly string REMOTE_ADDR;

	}
}
