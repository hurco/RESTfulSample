using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace WcfDataService.RESTful
{
    public class RESTsubscription
    {
        public enum SubscriptionMode { Webhook, Callback }
    }

    [Flags]
    public enum VendorAccess { Read=1, Write=2 }

    [DataContract]
    public class DoubleCommandData
    {
        [DataMember]
        public Double data { get; set; }
    }
    [DataContract]
    public class StringCommandData
    {
        [DataMember]
        public String data { get; set; }
    }
    [DataContract]
    public class IntegerCommandData
    {
        [DataMember]
        public int data { get; set; }
    }
    [DataContract]
    public class SubscriptionCommandData
    {
        [DataMember]
        public bool useEvent { get; set; }
        [DataMember]
        public bool useDatastore { get; set; }
        [DataMember]
        public RESTsubscription.SubscriptionMode mode { get; set; }
    }

    [DataContract]
    public class CallbackCommandData
    {
        [DataMember]
        public string webHook { get; set; }

        [DataMember]
        public bool useWebHook { get; set; }

    }

    [DataContract]
    public class NotificationData
    {
        [DataMember]
        public string SID { get; set; }

        [DataMember]
        public string SIDvalue { get; set; }

    }

    [DataContract]
    public class LoginCommandData
    {
        [DataMember]
        public string username { get; set; }
        [DataMember]
        public string password { get; set; }
    }

    [DataContract]
    public class TokenResponse
    {
        [DataMember]
        public string token { get; set; }
        [DataMember]
        public VendorAccess tokenPriveleges { get; set; }
    }

    [DataContract]
    public class Response
    {
        [DataMember]
        public object[] responseData { get; set; }
        [DataMember]
        public int dataCount { get; set; }
    }
}
