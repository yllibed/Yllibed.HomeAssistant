using System.Net.Http;
using System.Threading.Tasks;

namespace Yllibed.HomeAssistantFramework.WebServices
{
    public interface IHassWebApiServiceProxy
    {
        /// <summary>
        /// The *untyped* version used to post to services in the Hass WebApi. Throws exception if response is unsuccessful!
        /// TODO: Domain and Service should be an enum to get a nice typed experience.
        /// </summary>
        /// <param name="domain">Example: light, notify, switch</param>
        /// <param name="service">Example: turn_on, turn_off, toggle</param>
        /// <param name="data">Whatever object. It will be serialized to Json and sent in the message.</param>
        /// <exception cref="HttpRequestException">Can be thrown from response.EnsureSuccessStatusCode()</exception>
        /// <returns>Response from the call.</returns>

        Task<string> CallHassService(string domain, string service, object data);
        /// <summary>
        /// Gets all state data for a specified entity. Hass WebApi. Throws exception if response is unsuccessful! Be sure to catch 404's.
        /// </summary>
        /// <exception cref="HttpRequestException">Can be thrown from response.EnsureSuccessStatusCode()</exception>
        /// <returns>Response from the call.</returns>
        Task<string> GetHassEntityStateAsJson(string entityId);
    }
}