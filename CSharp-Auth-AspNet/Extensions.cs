using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Delight.Shim;
using Delight.Auth;
using System.Runtime.CompilerServices;

namespace Delight.Shim.AspNetCore
{
	public static class Extensions
	{
		/// <summary>
		/// Uses the ASP.NET http context to establish both the cookie manager and the client IP address
		/// </summary>
		/// <param name="AB"></param>
		/// <param name="Ctx"></param>
		/// <returns></returns>
		public static Delight.Auth.Auth.AuthBuilder SetEnvironment(this Delight.Auth.Auth.AuthBuilder AB, Microsoft.AspNetCore.Http.HttpContext Ctx)
		{
			AB.SetCookieManager((_PhpInstance) => new AspNetCookieMgr(_PhpInstance, Ctx));
			AB.SetClientIp(Ctx.Request.GetRemoteIp());
			return AB;
		}

		public static System.Net.IPAddress GetRemoteIp(this Microsoft.AspNetCore.Http.HttpRequest Request)
		{
			if (Request.Headers.TryGetValue("X-Forwarded-For", out Microsoft.Extensions.Primitives.StringValues XForwardedFor))
			{
				var S = (string)XForwardedFor;
				if (!string.IsNullOrEmpty(S))
				{
					var S2 = S.Split(',').Last().Trim();
					try
					{
						// X-Forwarded-For can contain multiple comma-separated IP addresses
						// e.g. X-Forwarded-For: client, proxy1, proxy2
						// The client can be forged; the only thing we trust is the last proxy in the last
						// since that's the one that our Apache proxy will have tacked on.
						return System.Net.IPAddress.Parse(S2);
					}
					catch (Exception exc)
					{
						throw new FormatException($"Error parsing X-Forwarded-For value '{S2}' (from '{S}').", exc);
					}
				}
			}
			return Request.HttpContext.Connection.RemoteIpAddress;
		}
	}
}
