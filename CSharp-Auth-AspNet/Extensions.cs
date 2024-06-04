using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Delight.Shim;
using Delight.Auth;
using System.Runtime.CompilerServices;
//using CSharp_Auth_AspNet;

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
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public static Delight.Auth.Auth.AuthBuilder SetEnvironment(this Delight.Auth.Auth.AuthBuilder AB, Microsoft.AspNetCore.Http.HttpContext Ctx, bool createSession)
		{
			AB.SetCookieManager((_PhpInstance) => new AspNetCookieMgr(_PhpInstance, Ctx));
			AB.SetClientIp(Ctx.Request.GetRemoteIp());

			if (createSession)
			{
				var bCookie = Ctx.Request.Cookies.TryGetValue(SessionIdCookieName, out string sessId);
				_SESSION outSess = null;
				var bSess = (bCookie ? ActiveSessions.TryGetValue(sessId, out outSess) : false);
				if (bSess)
				{
					AB.SetSessionManager(outSess);
				}
				else
				{
					AB.SetSessionManager((_PhpInstance) =>
					{
						var newSess = new _SESSION();
						var newSessId = Guid.NewGuid().ToString();
						ActiveSessions.Add(newSessId, newSess);
						var newCookie = new Cookie.Cookie(SessionIdCookieName, _PhpInstance);
						newCookie.setValue(newSessId);
						_PhpInstance._COOKIE.Set(SessionIdCookieName, newCookie);
						return newSess;
					});
				}
			}

			return AB;
		}
		private static Dictionary<string, Delight.Shim._SESSION> ActiveSessions = new Dictionary<string, Delight.Shim._SESSION>();
		private const string SessionIdCookieName= "CSAuthSessionId";

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
