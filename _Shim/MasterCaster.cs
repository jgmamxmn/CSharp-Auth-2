using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delight.Shim
{
	/// <summary>
	/// Who runs Bartertown?
	/// </summary>
	public static class MasterCaster
	{
		public static int GetInt(object o)
		{
			if (o is null) return 0;
			if (o is int i) return i;
			if (o is uint ui) return (int)ui;
			if (o is short s) return (int)s;
			if (o is ushort us) return (int)us;
			if (o is double d) return (int)d;
			if (o is float f) return (int)f;
			if (o is decimal dec) return (int)dec;
			if (o is long l) return (int)l;
			if (o is ulong ul) return (int)ul;
			if (o is byte b) return (int)b;
			if (o is sbyte sb) return (int)sb;
			if (o is char c) return (int)c;
			if (o is bool bl) return (bl ? 1 : 0);
			if (o is string str && int.TryParse(str, out int parsed)) return parsed;
			return 0;
		}
		public static double GetDbl(object o)
		{
			if (o is null) return 0;
			if (o is int i) return (double)i;
			if (o is uint ui) return (double)ui;
			if (o is short s) return (double)s;
			if (o is ushort us) return (double)us;
			if (o is double d) return d;
			if (o is float f) return (double)f;
			if (o is decimal dec) return (double)dec;
			if (o is long l) return (double)l;
			if (o is ulong ul) return (double)ul;
			if (o is byte b) return (double)b;
			if (o is sbyte sb) return (double)sb;
			if (o is char c) return (double)c;
			if (o is bool bl) return (bl ? 1.0 : 0.0);
			if (o is string str && double.TryParse(str, out double parsed)) return parsed;
			return 0;
		}
		public static float GetFloat(object o)
		{
			if (o is null) return 0;
			if (o is int i) return (float)i;
			if (o is uint ui) return (float)ui;
			if (o is short s) return (float)s;
			if (o is ushort us) return (float)us;
			if (o is double d) return (float)d;
			if (o is float f) return f;
			if (o is decimal dec) return (float)dec;
			if (o is long l) return (float)l;
			if (o is ulong ul) return (float)ul;
			if (o is byte b) return (float)b;
			if (o is sbyte sb) return (float)sb;
			if (o is char c) return (float)c;
			if (o is bool bl) return (bl ? 1F : 0F);
			if (o is string str && float.TryParse(str, out float parsed)) return parsed;
			return 0;
		}
		public static string GetString(object o)
		{
			if (o is null) return "";
			if (o is string str) return str;
			return o.ToString();
		}
	}
}
