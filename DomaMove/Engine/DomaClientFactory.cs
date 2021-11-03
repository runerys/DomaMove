﻿using System.ServiceModel;
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
                ReaderQuotas = XmlDictionaryReaderQuotas.Max,
                AllowCookies = true,
                //BypassProxyOnLocal = false,
                //UseDefaultWebProxy = false,
                //ProxyAddress = new System.Uri("http://127.0.0.1:8888")
            };

            if (serviceUri.Trim().ToLower().StartsWith("https:"))
                binding.Security = new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport };

            return new DOMAServicePortTypeClient(binding, new EndpointAddress(serviceUri));
        }
    }
}
