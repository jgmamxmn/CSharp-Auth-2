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

		public readonly string REMOTE_ADDR;
	}
}
