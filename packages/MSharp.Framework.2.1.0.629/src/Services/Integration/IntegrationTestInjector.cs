using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSharp.Framework.Services
{
    class IntegrationTestInjector
    {
        internal static void Inject(string serviceName, string request, string response)
        {
            throw new NotImplementedException();
            // TODO: Find the service.
            //var serviceInfo = IntegrationManager.IntegrationServices.FirstOrDefault(x => x.Value.Name == serviceName);

            //if (serviceInfo.Value == null) throw new Exception("No service of type " + serviceName + " is registered.");

            //var
        }
    }

    //class IntegrationServiceInfo
    //{
    //    public Type RequestType, ResponseType, ServiceType;
    //}
}
