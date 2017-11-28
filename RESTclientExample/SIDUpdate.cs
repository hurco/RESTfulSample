using System;

namespace RESTclient
{
    public class SIDUpdate : EventArgs
    {
        public string SID { get; set; } = "";
        public string SIDValue { get; set; } = "";
    }
}