using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuth.Shim
{
	public abstract class BasicDictionaryWrapped<KeyType, ValType>
	{
		internal Dictionary<KeyType, ValType> Dict;
		protected BasicDictionaryWrapped()
		{
			Dict = new Dictionary<KeyType, ValType>();
		}
		public virtual ValType this[KeyType key]
		{
			get
			{
				if (Dict.TryGetValue(key, out ValType ret))
					return ret;
				return default(ValType);
			}
			set
			{
				if (Dict.ContainsKey(key))
					Dict.Remove(key);
				Dict.Add(key, value);
			}
		}
		public virtual void unset(KeyType key)
		{
			if (Dict.ContainsKey(key))
				Dict.Remove(key);
		}
	}

	public interface _COOKIE : Php.Issetable
	{
		CSharpAuth.Cookie.Cookie Get(string key);
		bool TryGetValue(string key, out CSharpAuth.Cookie.Cookie cookieEntry);
		void Set(string key, CSharpAuth.Cookie.Cookie cookieEntry);
		void Unset(string key);
		void Clear();
		Dictionary<string, CSharpAuth.Cookie.Cookie> GetLiveCollection();
	}

	public class EmulatedCookieMgr : BasicDictionaryWrapped<string,CSharpAuth.Cookie.Cookie>, _COOKIE
	{
		public EmulatedCookieMgr() : base()
		{
		}
		public Dictionary<string, CSharpAuth.Cookie.Cookie> GetLiveCollection() => Dict;
		public virtual void Set(string key, CSharpAuth.Cookie.Cookie cookieEntry)
		{
			if (Dict.ContainsKey(key))
				Dict.Remove(key);
			Dict.Add(key, cookieEntry);
		}
		public virtual void Unset(string key)
		{
			if (Dict.ContainsKey(key))
				Dict.Remove(key);
		}
		public virtual CSharpAuth.Cookie.Cookie Get(string key) => Dict[key];
		public bool TryGetValue(string key, out CSharpAuth.Cookie.Cookie cookieEntry) => Dict.TryGetValue(key, out cookieEntry);
		public void Clear() => Dict.Clear();
		public bool isset(string key) => Dict.ContainsKey(key);
	}
}
