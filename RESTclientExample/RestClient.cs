using System;
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

        public bool ReUseConnections = false;

        private bool Abort = false;
        public RestClient(string address, string vendorID, string password)
        {
            this.address = address;
            this.password = password;
            this.vendorID = vendorID;

            // Override automatic validation of SSL server certificates.
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertficate;
        }

        public void Shutdown()
        {
            Abort = true;
            if (callbackstream != null)
            {
                callbackstream.Close();
            }
            Disconnect();
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

            if (cert.GetCertHashString() == "98D003D224D5D50F90CA4EBA311E7D995F03D33B")//ceck self signed hash
            {
                return true;
            }

            // Return true => allow unauthenticated server,
            //        false => disallow unauthenticated server.
            return false;
        }

        ~RestClient()
        {
            Shutdown();
        }

        public String Token { get { return token; } }


        private void ReadResponse(Stream source, byte[] buffer, int content_length, int already_done = 0)
        {
            int read_so_far = already_done;
            try
            {
                while (read_so_far < content_length - 1)
                {
                    read_so_far += source.Read(buffer, read_so_far, content_length - read_so_far);
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

        public void Disconnect()
        {
            HttpWebRequest get = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/AuthService/v1.2/Disconnect/");
            get.Method = "GET";
            get.Accept = "application/json, text/json";
            InitializeRequest(get);
            string value = "";
            try
            {
                String response = ExecuteRequest(get, Retries);
                connected = false;
            }
            catch (Exception err)
            {
                //might fail if older service
            }
        }

        public void ResumeConnection(Guid token)
        {
            if(connected)
            {
                Shutdown();
            }
            this.token = token.ToString();
            callbackthread = new Thread(() =>
               EnableCallback());
            callbackthread.Start();
            this.connected = true;
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
            login.ContentType = "application/json";
            

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
                                s.Close();
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

                success = waithandle.WaitOne(10000);
            }
            catch (Exception e)
            {
                success = false;
            }

            if (success && token != "")
            {
                callbackthread = new Thread(() =>
                EnableCallback());
                callbackthread.Start();
                connected = true;
            }

            return success;
        }

        public void RecoverCallbacks()
        {
            if(callbackthread!=null && callbackthread.IsAlive)
            {
                Abort = true;
                callbackstream.Close();
                callbackthread.Join();
            }
            connected = false;
            Abort = false;
            if (token != "")
            {
                callbackthread = new Thread(() =>
                EnableCallback());
                callbackthread.Start();
                connected = true;
            }
        }
        private Stream callbackStream = null;
        private Thread callbackthread = null;
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
                if (response == "STARTTLS")
                {
                    SslStream ssl = new SslStream(client.GetStream(), false, ValidateServerCertficate);

                    ssl.AuthenticateAsClient("machine-connect.hurco.com");
                    writer = new StreamWriter(ssl);
                    writer.WriteLine(token);
                    writer.Flush();
                    callbackStream = ssl;
                    callbackthread = new Thread(() => CallbackThread(ssl));
                    callbackthread.Start();
                }
                else
                {
                    int x = Thread.CurrentThread.ManagedThreadId;
                    writer.WriteLine(token);
                    writer.Flush();
                    callbackStream = client.GetStream();
                    CallbackThread(client.GetStream());
                }
            }

        }

        private Stream callbackstream = null;
        void CallbackThread(Stream source)
        {
            callbackstream = source;
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
                    if (!source.CanRead)
                    {
                        break;
                    }
                    try
                    {
                        bytes = source.Read(Buffer, 0, Buffer.Length);

                    }
                    catch (IOException e)
                    {
                        if(!Abort)
                        {
                            Thread t = new Thread(() => { callbackthread = null; Thread.Sleep(200); Abort = false; RecoverCallbacks(); });
                            t.Start();
                            Abort = true;
                            break;
                        }
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
                try
                {
                    NotificationData result = (NotificationData)NotificationSerializer.ReadObject(resultdata);
                    OnSidUpdated(result.SID, result.SIDvalue);
                }
                catch
                {
                    if (Abort)
                    {
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                
            }
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
        public void Subscribe(string sid)
        {
            Subscribe(sid, false);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
        public void Subscribe(string sid, bool HighFrequencySubscription)
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest subscription = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/NotificationService/Subscription/" + sid);
            subscription.Method = "POST";
            SubscriptionCommandData command = new SubscriptionCommandData() { mode = RESTsubscription.SubscriptionMode.Callback, useEvent = HighFrequencySubscription, useDatastore = true };
            MemoryStream payloaddata = new MemoryStream();
            SubscriptionSerializer.WriteObject(payloaddata, command);
            InitializeRequest(subscription, payloaddata);
            ExecuteRequest(subscription, Retries, payloaddata);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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
            subscription.Accept = "application/json, text/json";
            InitializeRequest(subscription);
            ExecuteRequest(subscription, Retries);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
        public void BeginSubscribe()
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest get = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/NotificationService/BeginSubscribe/");
            get.Method = "GET";
            get.Accept = "application/json, text/json";
            InitializeRequest(get);
            string value = "";
            try
            {
                String response = ExecuteRequest(get, Retries);
            }
            catch (Exception err)
            {
                //might fail if older service
            }
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
        public void EndSubscribe()
        {
            if (!connected)
            {
                if (!Connect())
                {
                    return;
                }
            }
            HttpWebRequest get = (HttpWebRequest)WebRequest.Create("https://" + address + ":4504/NotificationService/EndSubscribe/");
            get.Method = "GET";
            get.Accept = "application/json, text/json";
            InitializeRequest(get);
            string value = "";
            try
            {
                String response = ExecuteRequest(get, Retries);
            }
            catch (Exception err)
            {
                //might fail if older service
            }
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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
            get.Accept = "application/json, text/json";
            InitializeRequest(get);
            string value = "";
            String response = ExecuteRequest(get, Retries);
            if (response != null)
            {
                value = response;
            }
            return value;
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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
            get.Accept = "application/json, text/json";
            InitializeRequest(get);
            double value = 0.0;
            String response = ExecuteRequest(get, Retries);
            double.TryParse(response, out value);
            return value;
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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
            get.Accept = "application/json, text/json";
            InitializeRequest(get);
            int value = 0;
            String response = ExecuteRequest(get, Retries);
            int.TryParse(response, out value);
            return value;
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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

            ExecuteRequest(set, Retries, payloaddata);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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

            ExecuteRequest(set, Retries, payloaddata);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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

            ExecuteRequest(set, Retries, payloaddata);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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
            BulkSerializer.WriteObject(payloaddata, bulk);
            set.Method = "PUT";
            InitializeRequest(set, payloaddata);

            ExecuteRequest(set, Retries, payloaddata);
        }

        /// <exception cref="TimeoutException">retry count exceeded due to timeouts or other network faults</exception>
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
            set.Accept = "application/json, text/json";
            InitializeRequest(set);
            String json = ExecuteRequest(set,Retries);
            return (BulkWrapper)BulkSerializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        }

        private void InitializeRequest(HttpWebRequest set, MemoryStream payloaddata = null)
        {
            set.ServicePoint.Expect100Continue = false;
            set.Host = "machine-connect.hurco.com";
            set.KeepAlive = ReUseConnections;
            set.Pipelined = false;
            set.Timeout = Timeout.Infinite;
            set.SendChunked = false;
            set.ContentLength = 0;
            set.ConnectionGroupName = "WinmaxDataServices";
            set.Headers.Add("token", token);
            if (payloaddata != null)
            {
                set.ContentType = "application/json";
                set.ContentLength = payloaddata.Length;
            }
        }

        private HttpWebRequest CopyRequest(HttpWebRequest therequest)
        {
            HttpWebRequest copy = (HttpWebRequest)WebRequest.Create(therequest.RequestUri);
            copy.ServicePoint.Expect100Continue = therequest.ServicePoint.Expect100Continue;
            copy.Host = therequest.Host;
            copy.KeepAlive = therequest.KeepAlive;
            copy.Pipelined = therequest.Pipelined;
            copy.Timeout = therequest.Timeout;
            copy.SendChunked = therequest.SendChunked;
            copy.ContentLength = therequest.ContentLength;
            copy.ConnectionGroupName = therequest.ConnectionGroupName;
            copy.Headers.Add("token", therequest.Headers["token"]);
            copy.ContentType = therequest.ContentType;
            copy.Method = therequest.Method;
            copy.Expect = therequest.Expect;
            return copy;
        }

        private void ResetConnections()
        {
            ServicePoint srvrPoint = ServicePointManager.FindServicePoint(new Uri("https://" + address + ":4504/"));
            srvrPoint.CloseConnectionGroup("WinmaxDataServices");
        }

        public int RequestTimeoutMs { get; set; } = 3000;

        private bool WaitAndReset(EventWaitHandle waithandle, EventWaitHandle failhandle, ref bool timeout)
        {
            timeout = false;
            int result = EventWaitHandle.WaitAny(new WaitHandle[] { waithandle, failhandle }, RequestTimeoutMs);
            if (result == EventWaitHandle.WaitTimeout)
            {
                ResetConnections();
                timeout = true;
                return false;
            }
            if(result == 1) // fail event
            {
                ResetConnections();
                return false;
            }
            return true;
        }

        public int Retries { get; set; } = 10;

        private String ExecuteRequest(HttpWebRequest therequest, int retries, MemoryStream payloaddata = null)
        {
            //lock (this)
            {
                EventWaitHandle waithandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                EventWaitHandle failhandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                String Response = "";

                AsyncCallback ResponseHandler = (aresult) =>
                {
                    System.Diagnostics.Debug.WriteLine("Response handler begin");
                    HttpWebRequest request = (HttpWebRequest)aresult.AsyncState;
                    HttpWebResponse R = null;
                    try
                    {
                        R = (HttpWebResponse)request.EndGetResponse(aresult);
                        System.Diagnostics.Debug.WriteLine("Got a response");
                    }
                    catch (WebException e)
                    {
                        if (e.Status != WebExceptionStatus.RequestCanceled)
                        {
                            failhandle.Set();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Request aborted due to timeout");
                        }
                    }
                    if (R == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Response null");
                        return;
                    }
                    if (R.ContentLength > 0 && R.StatusCode == HttpStatusCode.OK)
                    {
                        System.Diagnostics.Debug.WriteLine("Response has payload begin reading it");
                        byte[] buffer = new byte[R.ContentLength];
                        try
                        {
                            R.GetResponseStream().BeginRead(buffer, 0, (int)R.ContentLength, (async) =>
                            {
                                System.Diagnostics.Debug.WriteLine("got response stream read bytes");
                                Stream s = (Stream)async.AsyncState;
                                try
                                {
                                    int bytes = s.EndRead(async);
                                    if (bytes < R.ContentLength)
                                    {
                                        ReadResponse(s, buffer, (int)R.ContentLength, bytes);
                                    }
                                    
                                    Response = Encoding.UTF8.GetString(buffer);
                                    System.Diagnostics.Debug.WriteLine("got bytes set function result:"+ Response);
                                    //Response r = (Response)ResponseSerializer.ReadObject(new MemoryStream(buffer));
                                    //Response = r.responseData[0].ToString();
                                    s.Close();
                                    R.Close();
                                    waithandle.Set();
                                }
                                catch(Exception e)
                                {
                                    System.Diagnostics.Debug.WriteLine("Failed to read bytes:" + e);
                                    s.Close();
                                    R.Close();
                                    failhandle.Set();
                                }
                            }, R.GetResponseStream());
                        }
                        catch
                        {
                            System.Diagnostics.Debug.WriteLine("failed to get response stream");
                            R.Close();
                            failhandle.Set();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("no response payload");
                        waithandle.Set();
                        R.Close();
                    }
                };

                try
                {
                    if (payloaddata != null)
                    {
                        therequest.BeginGetRequestStream((r) =>
                        {
                            HttpWebRequest webRequest = (HttpWebRequest)r.AsyncState;
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("Request initiated sending payload");
                                Stream stream = webRequest.EndGetRequestStream(r);
                                stream.Write(payloaddata.GetBuffer(), 0, (int)payloaddata.Length);
                                System.Diagnostics.Debug.WriteLine("payload sent, initiating wait for response");
                                therequest.BeginGetResponse(ResponseHandler, therequest);
                                stream.Flush();
                                stream.Close();
                                System.Diagnostics.Debug.WriteLine("request stream closed");
                            }
                            catch
                            {
                                
                                failhandle.Set();
                            }

                        }, therequest);
                    }
                    else
                    {

                        System.Diagnostics.Debug.WriteLine("initiating wait for reponse no payload to send");
                        IAsyncResult Aresult =(IAsyncResult)therequest.BeginGetResponse(ResponseHandler, therequest);

                    }
                    bool timeout = false;
                    if (!WaitAndReset(waithandle, failhandle, ref timeout) && retries>0)
                    {
                        if (timeout)
                        {
                            therequest.Abort();
                        }
                        System.Diagnostics.Debug.WriteLine("Request failed or timed out retrying, count="+(Retries-retries+1));
                        HttpWebRequest deepCopiedWebRequest = CopyRequest(therequest);
                        Response = ExecuteRequest(deepCopiedWebRequest, --retries, payloaddata);
                    }
                    if(retries<=0)
                    {
                        System.Diagnostics.Debug.WriteLine("Retry Count Exceeded!");
                        throw new TimeoutException("Retry count exceeded!");
                    }
                }
                catch (WebException e)
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                    if (e.Status != WebExceptionStatus.RequestCanceled)
                    {
                        therequest.Abort();
                        return Response;
                    }
                }
                catch (IOException e)
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                    therequest.Abort();
                    return Response;
                }
                return Response;
            }
        }

        
    }
}
