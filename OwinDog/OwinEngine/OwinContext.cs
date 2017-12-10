using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OwinEngine
{
    internal class OwinContext : IDictionary<string, object>, ICollection<KeyValuePair<string, object>>, IEnumerable<KeyValuePair<string, object>>, IEnumerable
    {
        private readonly Dictionary<string, object> parametersDictionary;

        public IDictionary<string, string[]> RequestHeaders { get; set; }

        public IDictionary<string, string[]> ResponseHeaders { get; set; }


        public OwinContext(string owinVersion, CancellationToken token, IDictionary<string, string[]> reqHeaders)
        {
            if (owinVersion != "1.0")
            {
                throw new ArgumentException("Owin Version must be equal to '1.0'");
            }
            parametersDictionary = new Dictionary<string, object>(30);
            if (reqHeaders != null)
            {
                RequestHeaders  = reqHeaders;
            }
            else
            {
                RequestHeaders = new HeaderDictionary();
            }
            ResponseHeaders = new HeaderDictionary();
            Set("owin.RequestHeaders", RequestHeaders);
            Set("owin.ResponseHeaders", ResponseHeaders);
            Set("owin.Version", owinVersion);
            Set("owin.CallCancelled", token);
            Set("owin.RequestScheme", "http");
        }



        public Stream RequestBody
        {
            get
            {
                return (Stream)parametersDictionary["owin.RequestBody"];
            }
            set
            {
                Set("owin.RequestBody", value);
            }
        }



        public Stream ResponseBody
        {
            get
            {
                return (Stream)parametersDictionary["owin.ResponseBody"];
            }
            set
            {
                Set("owin.ResponseBody", value);
            }
        }


        public void Add(KeyValuePair<string, object> item)
        {
            parametersDictionary.Add(item.Key, item.Value);
        }

        public void Add(string key, object value)
        {
            parametersDictionary.Add(key, value);
        }

        public void Clear()
        {
            parametersDictionary.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return parametersDictionary.ContainsKey(item.Key) && parametersDictionary[item.Key] == item.Value;
        }

        public bool ContainsKey(string key)
        {
            return parametersDictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string key)
        {
            return (T)((object)this[key]);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return parametersDictionary.GetEnumerator();
        }

        public string CompleteUri
        {
            get
            {
                string queryString = QueryString;
                return string.Concat(new string[]
			        {
				        Get<string>("owin.RequestScheme"),
				        "://",
				        RequestHeaders["Host"][0],
				        RelativePath,
				        (!string.IsNullOrEmpty(queryString)) ? ("?" + queryString) : null
			        });
            }
        }

        public int Count
        {
            get
            {
                return parametersDictionary.Count;
            }
        }


        public bool IsReadOnly
        {
            get { return false; }
        }

        public string HttpMethod
        {
            get
            {
                return Get<string>("owin.RequestMethod");
            }
        }

        public bool HttpMethodDefined
        {
            get
            {
                return ContainsKey("owin.RequestMethod");
            }
        }



        public object this[string key]
        {
            get { return parametersDictionary[key]; }
            set { parametersDictionary[key] = value; }
        }


        public ICollection<string> Keys
        {
            get
            {
                return parametersDictionary.Keys;
            }
        }




        public string QueryString
        {
            get
            {
                if (ContainsKey("owin.RequestQueryString"))
                {
                    return Get<string>("owin.RequestQueryString");
                }
                return null;
            }
        }

        public string RelativePath
        {
            get
            {
                return Get<string>("owin.RequestPathBase") + Get<string>("owin.RequestPath");
            }
        }

        public bool RelativePathDefined
        {
            get
            {
                return ContainsKey("owin.RequestPathBase") && ContainsKey("owin.RequestPath");
            }
        }



     
    


        public int ResponseStatusCode
        {
            get
            {
                if (ContainsKey("owin.ResponseStatusCode"))
                {
                    return Get<int>("owin.ResponseStatusCode");
                }
                return 200;
            }
            set
            {
                Set("owin.ResponseStatusCode", value);
            }
        }

        public string ResponseStatusCodeAndReason
        {
            get
            {
                int responseStatusCode = ResponseStatusCode;
                if (ContainsKey("owin.ResponseReasonPhrase"))
                {
                    string arg = Get<string>("owin.ResponseReasonPhrase");
                    return string.Format("{0} {1}", responseStatusCode, arg);
                }
                return responseStatusCode.ToString();
            }
            set
            {
                int num = value.IndexOf(" ");
                if (num == -1)
                {
                    ResponseStatusCode = (int.Parse(value));
                    return;
                }
                checked
                {
                    int responseStatusCode = int.Parse(value.Substring(0, num + 1));
                    string obj = value.Substring(num + 1);
                    ResponseStatusCode = (responseStatusCode);
                    Set("owin.ResponseReasonPhrase", obj);
                }
            }
        }

        public bool SomeResponseExists
        {
            get
            {
                return ContainsKey("owin.ResponseStatusCode") || ContainsKey("owin.ResponseReasonPhrase") || ResponseHeaders.Any<KeyValuePair<string, string[]>>() || (ContainsKey("owin.ResponseBody") && ((OwinResponseStream)ResponseBody).IsBeginWrite);
            }
        }

        public ICollection<object> Values
        {
            get
            {
                return parametersDictionary.Values;
            }
        }

        public bool Remove(string key)
        {
            return parametersDictionary.Remove(key);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return parametersDictionary.Remove(item.Key);
        }

        public void Set(string key, object obj)
        {
            if (parametersDictionary.ContainsKey(key))
            {
                parametersDictionary[key] = obj;
                return;
            }
            parametersDictionary.Add(key, obj);
        }


        public void SetResponseHeader(string headerName, string headerValue)
        {
            if (ResponseHeaders.ContainsKey(headerName))
            {
                ResponseHeaders[headerName][0] = headerValue;
                return;
            }
            ResponseHeaders.Add(headerName, new string[]
			{
				headerValue
			});
        }

        public void SetRequestHeader(string headerName, string headerValue)
        {
            if (RequestHeaders.ContainsKey(headerName))
            {
                RequestHeaders[headerName][0] = headerValue;
                return;
            }
            RequestHeaders.Add(headerName, new string[]
			{
				headerValue
			});
        }
       

        public bool TryGetValue(string key, out object value)
        {
            return parametersDictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
