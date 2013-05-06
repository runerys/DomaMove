using System.ServiceModel;
using System.Xml;
using DomaMove.Doma;

namespace DomaMove.Engine
{
    public class DomaClientFactory
    {
        public virtual DOMAServicePortType Create(string serviceUri)
        {
            var binding = new BasicHttpBinding
            {
                MaxBufferPoolSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                Security = new BasicHttpSecurity { Mode = BasicHttpSecurityMode.None },
                ReaderQuotas = new XmlDictionaryReaderQuotas
                {
                    MaxArrayLength = int.MaxValue,
                    MaxDepth = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                }
            };            
         
            return new DOMAServicePortTypeClient(binding, new EndpointAddress(serviceUri));
        }
    }
}
