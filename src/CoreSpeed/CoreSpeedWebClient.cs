using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace CoreSpeed
{
    internal class CoreSpeedWebClient : HttpClient
    {
        public int ConnectionLimit { get; set; } = 10;
        public new int Timeout {
            get
            {
                return this.Timeout;
            }

            set
            {
                base.Timeout = new TimeSpan(0, 0, value);
            }
        }

        public CoreSpeedWebClient()
        {
            this.Timeout = 60;
            //DefaultRequestHeaders.Add("userAgent", "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko");
            DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0");
            //DefaultRequestHeaders.Add("accept", "text/xml");
            CacheControlHeaderValue cc = new CacheControlHeaderValue();
            //cc.NoCache = false;
            //DefaultRequestHeaders.CacheControl = cc;
        }

        public CoreSpeedWebClient(int TimeOutSeconds)
        {
            this.Timeout = TimeOutSeconds;
        }

        public T GetConfig<T>(string Url)
        {
            var data = GetWebRequest(Url).Result;

            var xmlSerializer = new XmlSerializer(typeof(T));

            if (data != null && data.Length > 0)
            {

                using (var reader = new StringReader(data.ToString()))
                {
                    return (T)xmlSerializer.Deserialize(reader);
                }
            }
            else
            {
                throw new Exception("The data is blank");
            }
        }

        public async Task<string> GetWebRequest(string Url)
        {
            string content = "";

            Uri uri = AddTimeStampToUrl(new Uri(Url));
            HttpResponseMessage response = await GetAsync(uri);            
            
            if (response.IsSuccessStatusCode)
            {
                content = await response.Content.ReadAsStringAsync();                
            }

            return content;
        }

        private static Uri AddTimeStampToUrl(Uri address)
        {
            var uriBuilder = new UriBuilder(address);
            var query = uriBuilder.Query;

            query = "x=";
            query += DateTime.Now.ToFileTime().ToString(CultureInfo.InvariantCulture);
            uriBuilder.Query = query.ToString();
            return uriBuilder.Uri;   
        }
    }
}
