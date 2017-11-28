﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using WcfDataService;
using WcfDataService.RESTful;

namespace RESTclient
{
    public class RestClient
    {

        private string vendorID = "";
        private string password = "";
        private string address = "";

        private string token = "";

        private bool connected = false;
        DataContractJsonSerializer DoubleSerializer = new DataContractJsonSerializer(typeof(DoubleCommandData));
        DataContractJsonSerializer IntegerSerializer = new DataContractJsonSerializer(typeof(IntegerCommandData));
        DataContractJsonSerializer StringSerializer = new DataContractJsonSerializer(typeof(StringCommandData));
        DataContractJsonSerializer SubscriptionSerializer = new DataContractJsonSerializer(typeof(SubscriptionCommandData));
        DataContractJsonSerializer CallbackSerializer = new DataContractJsonSerializer(typeof(CallbackCommandData));
        DataContractJsonSerializer NotificationSerializer = new DataContractJsonSerializer(typeof(NotificationData));
        DataContractJsonSerializer LoginSerializer = new DataContractJsonSerializer(typeof(LoginCommandData));
        DataContractJsonSerializer BulkSerializer = new DataContractJsonSerializer(typeof(BulkWrapper));
        DataContractJsonSerializer TokenSerializer = new DataContractJsonSerializer(typeof(TokenResponse));
        DataContractJsonSerializer ResponseSerializer = new DataContractJsonSerializer(typeof(Response));

        public event EventHandler<SIDUpdate> SidUpdated;

        public bool IsConnected { get { return connected; } }

        private bool Abort = false;
        public RestClient(string address, string vendorID, string password)
        {
            this.address = address;
            this.password = password;
            this.vendorID = vendorID;

            // Override automatic validation of SSL server certificates.
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertficate;
            


        }

        private bool ValidateServerCertficate(
                object sender,
                X509Certificate cert,
                X509Chain chain,
                SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                // Good certificate.
                return true;
            }

            if(cert.GetCertHashString() == "98D003D224D5D50F90CA4EBA311E7D995F03D33B")//ceck self signed hash
            {
                return true;
            }

            // Return true => allow unauthenticated server,
            //        false => disallow unauthenticated server.
            return false;
        }

        ~RestClient()
        {
            Abort = true;
        }

        public String Token { get { return token; } }


        private void ReadResponse(Stream source, byte[] buffer, int content_length)
        {
            int read_so_far = 0;
            try
            {
                while (read_so_far < content_length - 1)
                {
                    read_so_far += source.Read(buffer, read_so_far, content_length);
                }
            }
            catch (IOException err)
            {
                //stream closed
            }
        }

        private void OnSidUpdated(String SID, String Value)
        {
            SidUpdated?.Invoke(this, new SIDUpdate() { SID = SID, SIDValue = Value });
        }
        public bool Connect()
        {
            EventWaitHandle waithandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            HttpWebRequest login = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/AuthService/Connect");
            login.ServicePoint.Expect100Continue = false;
            login.Method = "POST";
            login.Host = "machine-connect.hurco.com";
            login.KeepAlive = false;
            
            login.Pipelined = false;
            bool success = false;
            LoginCommandData command = new LoginCommandData() { username = vendorID, password = password };
            MemoryStream payloaddata = new MemoryStream();
            LoginSerializer.WriteObject(payloaddata, command);
            login.ContentType = "text/json";

            login.ContentLength = payloaddata.Length;
            try
            {
                login.BeginGetRequestStream((r) =>
            {
                HttpWebRequest webRequest = (HttpWebRequest)r.AsyncState;
                Stream stream = webRequest.EndGetRequestStream(r);
                stream.Write(payloaddata.GetBuffer(), 0, (int)payloaddata.Length);

                login.BeginGetResponse((aresult) =>
                {
                    HttpWebRequest request = (HttpWebRequest)aresult.AsyncState;
                    HttpWebResponse R = null;
                    try
                    {
                        R = (HttpWebResponse)request.EndGetResponse(aresult);
                    }
                    catch (Exception e)
                    {
                        waithandle.Set();
                    }
                    if (R.ContentLength > 0 && R.StatusCode == HttpStatusCode.OK)
                    {
                        byte[] buffer = new byte[R.ContentLength];
                        R.GetResponseStream().BeginRead(buffer, 0, (int)R.ContentLength, (async) =>
                           {
                               Stream s = (Stream)async.AsyncState;
                               int bytes = s.EndRead(async);
                               if (bytes < R.ContentLength)
                               {
                                   ReadResponse(s, buffer, (int)R.ContentLength);
                               }
                               MemoryStream resultdata = new MemoryStream(buffer);
                               TokenResponse result = (TokenResponse)TokenSerializer.ReadObject(resultdata);
                               this.token = result.token;
                               R.Close();
                               waithandle.Set();
                           }, R.GetResponseStream());
                    }
                    else
                    {
                        waithandle.Set();
                    }
                    R.Close();
                    stream.Close();
                }, login);

            }, login);

                success=  waithandle.WaitOne(10000);
            }
            catch (Exception e)
            {
                success= false;
            }

            if(success && token!="")
            {
                Thread t = new Thread(() =>
                EnableCallback());
                t.Start();
                connected = true;
            }

            return success;
        }

        private void EnableCallback()
        {
            if (token != "")
            {
                EventWaitHandle waithandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                TcpClient client = new TcpClient();
                client.Connect(address, 4505);
                StreamWriter writer = new StreamWriter(client.GetStream());
                StreamReader reader = new StreamReader(client.GetStream());
                writer.WriteLine("STARTTLS");
                writer.Flush();
                String response = reader.ReadLine();
                if(response == "STARTTLS")
                {
                    SslStream ssl = new SslStream(client.GetStream(),false,ValidateServerCertficate);
                    
                    ssl.AuthenticateAsClient("machine-connect.hurco.com");
                    writer = new StreamWriter(ssl);
                    writer.WriteLine(token);
                    writer.Flush();
                    Thread t = new Thread(() => CallbackThread(ssl));
                    t.Start();
                }
                else
                {
                    int x = Thread.CurrentThread.ManagedThreadId;
                    writer.WriteLine(token);
                    writer.Flush();
                    CallbackThread(client.GetStream());
                }
            }

        }

        void CallbackThread(Stream source)
        {
            byte[] Buffer = new byte[8192];
            Decoder decoder = Encoding.UTF8.GetDecoder();
            while (!Abort)
            {
                Array.Clear(Buffer, 0, Buffer.Length);
                if (!source.CanRead)
                {
                    return;
                }
                StringBuilder messageData = new StringBuilder();
                int bytes = 0;
                
                char[] chars = new char[decoder.GetCharCount(Buffer, 0, bytes)];
                
                decoder.GetChars(Buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                do
                {
                    if(!source.CanRead)
                    {
                        break;
                    }
                    try
                    {
                        bytes = source.Read(Buffer, 0, Buffer.Length);

                    }
                    catch (Exception ex)
                    {
                    }
                    // Use Decoder class to convert from bytes to UTF8
                    // in case a character spans two buffers.

                    chars = new char[decoder.GetCharCount(Buffer, 0, bytes)];
                    decoder.GetChars(Buffer, 0, bytes, chars, 0);
                    messageData.Append(chars);
                    // Check for EOF.
                    if (messageData.ToString().IndexOf("\r\n") != -1)
                    {
                        break;
                    }
                    else if (bytes == 0)
                    {
                        Thread.Sleep(1);
                    }
                }
                while (bytes != -1);

                MemoryStream resultdata = new MemoryStream(Encoding.UTF8.GetBytes(messageData.ToString()));
                NotificationData result = (NotificationData)NotificationSerializer.ReadObject(resultdata);
                OnSidUpdated(result.SID, result.SIDvalue);
            }
        }

        public void Subscribe(string sid)
        {
            if(!connected)
            {
                if(!Connect())
                {
                    return;
                }
            }
            HttpWebRequest subscription = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/NotificationService/Subscription/" + sid);
            subscription.Method = "POST";
            SubscriptionCommandData command = new SubscriptionCommandData() { mode = RESTsubscription.SubscriptionMode.Callback, useEvent = false, useDatastore = true };
            MemoryStream payloaddata = new MemoryStream();
            SubscriptionSerializer.WriteObject(payloaddata, command);
            InitializeRequest(subscription, payloaddata);
            ExecuteRequest(subscription, payloaddata);
        }

        public void Unsubscribe(string sid)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest subscription = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/NotificationService/Subscription/" + sid);
            subscription.Method = "DELETE";
            InitializeRequest(subscription);
            ExecuteRequest(subscription);
        }


        public void BeginSubscribe() { }


        public void EndSubscribe() { }

        public String GetSID(string sidName)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return "";
                }
            }
            HttpWebRequest get = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/String/" + sidName);
            get.Method = "GET";
            InitializeRequest(get);
            string value = "";
            String response = ExecuteRequest(get);
            if(response!= null)
            {
                value = response;
            }
            return value;
        }

        public double GetDoubleSID(string sidName)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return 0;
                }
            }
            HttpWebRequest get = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/Double/" + sidName);
            get.Method = "GET";
            InitializeRequest(get);
            double value = 0.0;
            String response = ExecuteRequest(get);
            double.TryParse(response, out value);
            return value;
        }

        public int GetIntSID(string sidName)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return 0;
                }
            }
            HttpWebRequest get = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/Integer/" + sidName);
            get.Method = "GET";
            InitializeRequest(get);
            int value = 0;
            String response = ExecuteRequest(get);
            int.TryParse(response, out value);
            return value;
        }

        public void SetSID(String SID, double value)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest set = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/Double/" + SID);
            set.Method = "PUT";
            DoubleCommandData command = new DoubleCommandData() { data = value };
            MemoryStream payloaddata = new MemoryStream();
            DoubleSerializer.WriteObject(payloaddata, command);
            InitializeRequest(set, payloaddata);

            ExecuteRequest(set, payloaddata);
        }
        public void SetSID(String SID, string value)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest set = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/String/" + SID);
            set.Method = "PUT";
            StringCommandData command = new StringCommandData() { data = value };
            MemoryStream payloaddata = new MemoryStream();
            StringSerializer.WriteObject(payloaddata, command);
            InitializeRequest(set, payloaddata);

            ExecuteRequest(set, payloaddata);
        }
        public void SetSID(String SID, int value)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest set = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/Integer/" + SID);
            IntegerCommandData command = new IntegerCommandData() { data = value };
            MemoryStream payloaddata = new MemoryStream();
            IntegerSerializer.WriteObject(payloaddata, command);
            set.Method = "PUT";
            InitializeRequest(set, payloaddata);

            ExecuteRequest(set, payloaddata);
        }

        public void SetSID(String SID, BulkWrapper value)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest set = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/Bulk/" + SID);
            BulkWrapper bulk = value;
            MemoryStream payloaddata = new MemoryStream();
            IntegerSerializer.WriteObject(payloaddata, bulk);
            set.Method = "PUT";
            InitializeRequest(set, payloaddata);

            ExecuteRequest(set, payloaddata);
        }

        public BulkWrapper GetBulk(String SID)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return null;
                }
            }
            HttpWebRequest set = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/DataService/Bulk/" + SID);
            set.Method = "GET";
            InitializeRequest(set);

            String json = ExecuteRequest(set);
            return (BulkWrapper)BulkSerializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        }

        private void InitializeRequest(HttpWebRequest set, MemoryStream payloaddata = null)
        {
            set.ServicePoint.Expect100Continue = false;
            set.Host = "machine-connect.hurco.com";
            set.Headers.Add("token", token);
            if (payloaddata != null)
            {
                set.ContentType = "text/json";
                set.ContentLength = payloaddata.Length;
            }
        }

        private String ExecuteRequest(HttpWebRequest therequest, MemoryStream payloaddata = null)
        {
            EventWaitHandle waithandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            String Response = "";

            AsyncCallback ResponseHandler = (aresult) =>
            {
                HttpWebRequest request = (HttpWebRequest)aresult.AsyncState;
                HttpWebResponse R = null;
                try
                {
                    R = (HttpWebResponse)request.EndGetResponse(aresult);
                }
                catch (Exception e)
                {
                    waithandle.Set();
                }
                if (R == null)
                {
                    return ;
                }
                if (R.ContentLength > 0 && R.StatusCode == HttpStatusCode.OK)
                {
                    byte[] buffer = new byte[R.ContentLength];
                    R.GetResponseStream().BeginRead(buffer, 0, (int)R.ContentLength, (async) =>
                    {
                        Stream s = (Stream)async.AsyncState;
                        int bytes = s.EndRead(async);
                        if (bytes < R.ContentLength)
                        {
                            ReadResponse(s, buffer, (int)R.ContentLength);
                        }
                        Response = Encoding.UTF8.GetString(buffer);
                        //Response r = (Response)ResponseSerializer.ReadObject(new MemoryStream(buffer));
                        //Response = r.responseData[0].ToString();
                        R.Close();
                        waithandle.Set();
                    }, R.GetResponseStream());
                }
                else
                {
                    waithandle.Set();
                }
                R.Close();
            };

            try
            {
                if (payloaddata != null)
                {
                    therequest.BeginGetRequestStream((r) =>
                    {
                        HttpWebRequest webRequest = (HttpWebRequest)r.AsyncState;
                        Stream stream = webRequest.EndGetRequestStream(r);
                        stream.Write(payloaddata.GetBuffer(), 0, (int)payloaddata.Length);
                        therequest.BeginGetResponse(ResponseHandler, therequest);

                    }, therequest);
                }
                else
                {
                    therequest.BeginGetResponse(ResponseHandler, therequest);
                }

                waithandle.WaitOne(3000);
            }
            catch (Exception e)
            {
                return Response;
            }
            return Response;
        }

       
    }
}