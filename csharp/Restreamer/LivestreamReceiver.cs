using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Restreamer
{
    public class LivestreamReceiver
    {
        CancellationTokenSource canceller;
        HttpWebRequest request;
        HttpWebResponse response;
        Task listenerTask;

        public LivestreamReceiver(string url)
        {
            canceller = new CancellationTokenSource();

            request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Headers.Add("icy-metadata", "1");
            request.Timeout = 5000;
            request.UserAgent = "Restreamer/2.0";
        }

        public Uri SourceUri { get { return request.RequestUri; } }

        public Dictionary<string, string> Metadata { get; private set; }
        public Dictionary<string, string> Headers { get; private set; }
        public bool HasMetadata { get { return Headers.ContainsKey("icy-metaint"); } }
        public bool IsRunning { get { return listenerTask != null && (listenerTask.Status == TaskStatus.Running); } }

        public event EventHandler<LivestreamDataEventArgs> DataReceived;
        public event EventHandler<LivestreamMetadataEventArgs> MetadataChanged;

        public void Start()
        {
            response = (HttpWebResponse)request.GetResponse();
            listenerTask = Task.Factory.StartNew(() => _listen());
        }

        public void Stop()
        {
            canceller.Cancel();
            Metadata.Clear();
            Headers.Clear();
        }

        private void _setMetadata(string n, string v)
        {
            if (Metadata.ContainsKey(n))
                Metadata[n] = v;
            else
                Metadata.Add(n, v);
        }

        private void _listen()
        {
            Headers = new Dictionary<string, string>();
            foreach (string key in response.Headers.Keys)
            {
                // Ignore headers which are not needed for stream analysis
                if (!key.StartsWith("ice-") && !key.StartsWith("icy-") && !key.StartsWith("content-"))
                    continue;

                Headers.Add(key.ToLower(), response.Headers[key]);
            }

            if(HasMetadata)
                Metadata = new Dictionary<string, string>();

            ulong bufferSize = HasMetadata ? ulong.Parse(Headers["icy-metaint"]) : 8192 /* 8 kB */;
            byte[] buffer = new byte[bufferSize];
            Stream stream = response.GetResponseStream();
            int actualReadLength = 0;
            int packetLength = 0;

            while ((actualReadLength +=
                //stream.Read(buffer, actualReadLength, buffer.Length - actualReadLength)
                (packetLength = stream.Read(buffer, 0, buffer.Length - actualReadLength))
                ) > 0)
            {
                if (DataReceived != null)
                    DataReceived.Invoke(this, new LivestreamDataEventArgs(this, buffer.SubArray<byte>(0, packetLength)));

                if (actualReadLength < buffer.Length)
                    continue;

                // Reset stream counter
                actualReadLength = 0;

                if (HasMetadata)
                {
                    int length = stream.ReadByte() * 16; // TODO: Handle sudden stream fails by catching length < 0

                    if (length == 0)
                        continue;

                    byte[] metadatabuffer = new byte[length];
                    stream.Read(metadatabuffer, 0, length);
                    string rawmetadata = Encoding.UTF8.GetString(metadatabuffer).TrimEnd('\0'); // the metadata is padded with \0 bytes

                    // This is some clean code for metadata decoding. :3
                    while (rawmetadata.Length > 0)
                    {
                        // Get the name
                        string n = rawmetadata.Substring(0, rawmetadata.IndexOf('='));
                        rawmetadata = rawmetadata.Substring(rawmetadata.IndexOf('=') + 1);

                        // Quoted?
                        char delimitchar = ';';
                        if (
                            rawmetadata.StartsWith("'")
                            || rawmetadata.StartsWith("\"")
                            )
                        {
                            delimitchar = rawmetadata[0];
                            rawmetadata = rawmetadata.Substring(1);
                        }

                        // Get the value
                        int i = rawmetadata.Contains(delimitchar) ? rawmetadata.IndexOf(delimitchar) : rawmetadata.Length;
                        string v = rawmetadata.Substring(0, i);
                        rawmetadata = rawmetadata.Substring(i);

                        // Add to metadata
                        _setMetadata(n.Trim(), v);

                        // No metadata available anymore
                        if (rawmetadata.Length == 0)
                            break;

                        // Remove delimiter(s) from rest
                        if ((rawmetadata = rawmetadata.Substring(1))[0] == ';')
                            rawmetadata = rawmetadata.Substring(1);
                    }

                    if (MetadataChanged != null)
                        MetadataChanged.Invoke(this, new LivestreamMetadataEventArgs(this, this.Metadata));
                }
            }
        }
    }

    public class LivestreamDataEventArgs : EventArgs
    {
        public LivestreamReceiver Receiver { get; private set; }
        public byte[] Data { get; private set; }

        internal LivestreamDataEventArgs(LivestreamReceiver receiver, byte[] data)
        {
            this.Receiver = receiver;
            this.Data = data;
        }
    }

    public class LivestreamMetadataEventArgs : EventArgs
    {
        public LivestreamReceiver Receiver { get; private set; }
        public Dictionary<string, string> Metadata { get; private set; }

        public string CurrentArtist { get { return Metadata["StreamTitle"].Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries).First(); } }
        public string CurrentTitle { get { return Metadata["StreamTitle"].Substring(CurrentArtist.Length + 3); } }

        internal LivestreamMetadataEventArgs(LivestreamReceiver receiver, Dictionary<string, string> metadata)
        {
            this.Receiver = receiver;
            this.Metadata = metadata;
        }
    }
}
