using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Restreamer
{
    public class RestreamTarget
    {
        WebClient wc = new WebClient();

        public RestreamTarget(LivestreamReceiver receiver, Uri uri)
        {
            Receiver = receiver;
            UserAgent = string.Format("Restreamer/{0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            ServerUsername = "source";
            ServerPassword = "donothackme";
            ServerHost = "127.0.0.1";
            ServerPort = 8000;
            Mountpoint = "/"; // shoutcast
            Type = RestreamType.Icecast;
            RelayMetadata = true;

            if(!string.IsNullOrEmpty(uri.Host))
                ServerHost = uri.Host;

            if(!uri.IsDefaultPort)
                ServerPort = (ushort)uri.Port;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                if (uri.UserInfo.Contains(':'))
                {
                    // Username:Password
                    ServerUsername = uri.UserInfo.Substring(0, uri.UserInfo.IndexOf(':'));
                    ServerPassword = uri.UserInfo.Substring(uri.UserInfo.IndexOf(':') + 1);
                }
                else
                    // Default: password only. Quite weird for URIs, but I'm too lazy to make an alternative way.
                    ServerPassword = uri.UserInfo;
            }

            if (!string.IsNullOrEmpty(uri.AbsolutePath))
                Mountpoint = uri.AbsolutePath;

            var q = HttpUtility.ParseQueryString(uri.Query.TrimStart('?'));
            foreach (string k in q.AllKeys)
            {
                string v = q[k];
                switch (k.ToLower())
                {
                    case "relay-metadata":
                        RelayMetadata = bool.Parse(v);
                        break;
                    case "user-agent":
                        UserAgent = v;
                        break;
                    case "password": // same as password@host, but looks better to me.
                        ServerPassword = v;
                        break;
                }
            }

            switch (uri.Scheme.ToLower())
            {
                case "shout":
                case "sc":
                    Type = RestreamType.Shoutcast;
                    break;

                default:
                    Type = RestreamType.Icecast;
                    break;
            }

            receiver.DataReceived += new EventHandler<LivestreamDataEventArgs>(receiver_DataReceived);
            receiver.MetadataChanged += new EventHandler<LivestreamMetadataEventArgs>(receiver_MetadataChanged);
        }

        void receiver_MetadataChanged(object sender, LivestreamMetadataEventArgs e)
        {
            if (!IsConnected)
                return;

            if (e.Metadata == null)
                return;

            if (!e.Metadata.ContainsKey("StreamTitle"))
                return;

            Task.Factory.StartNew(() => _refreshMetadata());
        }
        void _refreshMetadata()
        {
            // TODO: Use UriBuilder, if needed.
            string uri = "";
            switch (Type)
            {
                case RestreamType.Shoutcast:
                    uri = string.Format(
                        "http://{2}@{0}:{1}/admin.cgi?pass={3}&mode=updinfo&song={4}",
                        ServerHost,
                        ServerPort,
                        Uri.EscapeDataString(ServerUsername),
                        Uri.EscapeDataString(ServerPassword),
                        Uri.EscapeDataString(Receiver.Metadata["StreamTitle"])
                    );
                    break;
                default:
                    uri = string.Format(
                        "http://{2}:{3}@{0}:{1}/admin/metadata?mount={4}&mode=updinfo&song={5}",
                        ServerHost,
                        ServerPort,
                        Uri.EscapeDataString(ServerUsername),
                        Uri.EscapeDataString(ServerPassword),
                        Mountpoint,
                        Uri.EscapeDataString(Receiver.Metadata["StreamTitle"])
                    );
                    break;
            }

            Console.WriteLine("{0}", uri);

            // Just do it and ignore errors
            try
            {
                wc.Headers["User-Agent"] = UserAgent;
                wc.DownloadString(uri);
            }
            catch (Exception erri)
            {
                Console.WriteLine("Warning: Could not apply metadata - Internal error: {0}", erri.Message);
            }
        }
        void receiver_DataReceived(object sender, LivestreamDataEventArgs e)
        {
            if (!IsConnected)
                return;

            try
            {
                stream.Write(e.Data, 0, e.Data.Length);
                stream.Flush();
            }
            catch (Exception err)
            {
                Console.WriteLine("Error: Could not write stream data - {0}", err.Message);

                Stop();
            }
        }

        public string ServerUsername { get; set; }
        public string ServerPassword { get; set; }
        public string ServerHost { get; set; }
        public ushort ServerPort { get; set; }
        public string UserAgent { get; set; }
        public string Mountpoint { get; set; }
        public RestreamType Type { get; set; }
        public bool RelayMetadata { get; set; }
        public LivestreamReceiver Receiver { get; private set; }
        public bool IsConnected { get { return sock != null && sock.Connected; } }

        public event EventHandler Connected;
        public event EventHandler Disconnected;

        private Socket sock;
        private NetworkStream stream;
        private StreamWriter sw;
        private StreamReader sr;

        public void Start()
        {
            Stop();

            // TODO: Implement support for IPv6
            Socket sm = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sm.Connect(ServerHost, ServerPort + (Type == RestreamType.Shoutcast ? 1 : 0)); // Shoutcast audio is sent on port <original-port>+1

            stream = new NetworkStream(sm);
            stream.WriteTimeout = 2000;
            stream.ReadTimeout = 2000;
            sr = new StreamReader(stream);
            sw = new StreamWriter(stream);

            sw.AutoFlush = true;

            // Authentication
            // different for each server type
            switch (Type)
            {
                case RestreamType.Shoutcast:
                    // Send our password
                    sw.WriteLine(ServerPassword);

                    // Get the status, expected: "OK2"
                    string status = sr.ReadLine();
                    if (status != "OK2")
                    {
                        sw.Close();
                        throw new Exception(string.Format("Could not connect: Status is {0}", status));
                    }

                    break;

                case RestreamType.Icecast:
                    sw.WriteLine("SOURCE {0} {1}", Mountpoint, "HTTP/1.0");
                    sw.WriteLine("Host: {0}", ServerHost);
                    sw.WriteLine("User-Agent: {0}", UserAgent);
                    foreach (string headerKey in Receiver.Headers.Keys)
                        if(!headerKey.Equals("icy-metaint", StringComparison.OrdinalIgnoreCase))
                            sw.WriteLine("{0}: {1}", headerKey, Receiver.Headers[headerKey]);
                    sw.WriteLine("Authorization: Basic {0}", Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", ServerUsername, ServerPassword))));
                    sw.WriteLine();

                    // TODO: Catch errors from icecast server
                    Console.WriteLine("Icecast2 Debug: RESPONSE = {0}", sr.ReadLine());
                    break;
            }

            string capline;
            while ((capline = sr.ReadLine()) != "")
            {
                string[] cap = capline.Split(':');
                Console.WriteLine("Received: {0} = {1}", cap[0], cap[1]);
            }

            Task.Factory.StartNew(() => _listen());
            stream.WriteTimeout = 300;

            sock = sm;
            receiver_MetadataChanged(Receiver, new LivestreamMetadataEventArgs(Receiver, Receiver.Metadata));

        }

        public void Stop()
        {
            if (IsConnected)
                sock.Close();
            sock = null;
        }

        private void _listen()
        {
            if (Connected != null)
                Connected.Invoke(this, new EventArgs());
            while (sock.Connected)
                Thread.Sleep(500);
            if (Disconnected != null)
                Disconnected.Invoke(this, new EventArgs());
        }
    }

    public enum RestreamType
    {
        Icecast = 0,
        Shoutcast = 1
    }
}
