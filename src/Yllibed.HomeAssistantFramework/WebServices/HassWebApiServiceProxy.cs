using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Yllibed.HomeAssistantFramework.WebServices
{
    public class HassWebApiServiceProxy : IHassWebApiServiceProxy
    {
        private static readonly HttpClient Client = new HttpClient();
        private static string _password_query;

        public HassWebApiServiceProxy(string webApiBaseUrl)
        {
            Client.BaseAddress = new Uri(webApiBaseUrl);
        }
        public HassWebApiServiceProxy(string webApiBaseUrl, string apiPassword = "")
        {
            Client.BaseAddress = new Uri(webApiBaseUrl);
            if (!string.IsNullOrEmpty(apiPassword))
            {
                _password_query = $"?api_password={apiPassword}";
            }
        }
        /// <inheritdoc />
        public async Task<string> GetHassEntityStateAsJson(string entityId)
        {
            var response = await Client.GetAsync($"api/states/{entityId}{_password_query}");

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException($"{response.RequestMessage.Method} {response.RequestMessage.RequestUri} - threw exception: {e.Message}", e);
            }

            // return URI of the created resource.
            return await response.Content.ReadAsStringAsync();
        }

        /// <inheritdoc />
        public async Task<string> CallHassService(string domain, string service, object data)
        {

            HttpResponseMessage response = await Client.PostAsJsonAsync($"api/services/{domain}/{service}{_password_query}", data);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException($"{response.RequestMessage.Method} {response.RequestMessage.RequestUri} with Content: {data.ToString()} - threw exception: {e.Message}", e);
            }


            // return URI of the created resource.
            return await response.Content.ReadAsStringAsync();
        }
    }
}
