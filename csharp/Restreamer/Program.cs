using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Restreamer
{
    class Program
    {
        static string configfile = null;
        static bool showversion = true;

        static List<RestreamTarget> targets = new List<RestreamTarget>();
        static List<LivestreamReceiver> receivers = new List<LivestreamReceiver>();

        static void Usage()
        {
        }

        static void Version()
        {
            Console.WriteLine("Restreamer {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("\tby Carl Kittelberger <icedream@blazing.de>");
        }

        static int Main(string[] args)
        {
            // Parse arguments
            Queue<string> arguments = new Queue<string>(args);
            while (arguments.Count > 0)
            {
                string name = arguments.Dequeue();
                switch (name)
                {
                    case "--no-logo":
                    case "--no-version":
                    case "-v-":
                        showversion = false;
                        break;

                    case "--config-file":
                    case "-c":
                        configfile = arguments.Dequeue();
                        break;

                    case "--help":
                    case "-h":
                        if (showversion)
                        {
                            Version();
                            Console.WriteLine();
                        }
                        Usage();
                        return 0;

                    case "--version":
                    case "-v":
                        Version();
                        return 0;
                }
            }

            if (showversion)
            {
                Version();
                Console.WriteLine();
            }

            if (string.IsNullOrEmpty(configfile))
            {
                Console.Error.WriteLine("You need to input a configuration XML file with the -c/--config-file parameter.");
                return -1;
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(configfile);
            }
            catch (XmlException e)
            {
                Console.Error.WriteLine("Error in XML configuration file:");
                Console.Error.WriteLine("  {0}", e.Message);
                Console.Error.WriteLine("in line {0}, pos {1}.", e.LineNumber, e.LinePosition);
                return -2;
            }

            // Init sources
            foreach (var sourceNode in doc.SelectNodes("//restreamer/source").Cast<XmlNode>())
            {
                LivestreamReceiver recv = new LivestreamReceiver(sourceNode.Attributes["uri"].Value);
                receivers.Add(recv);

                // Init targets
                foreach (var targetNode in sourceNode.SelectNodes("child::target").Cast<XmlNode>())
                {
                    RestreamTarget targ = new RestreamTarget(recv, new Uri(targetNode.Attributes["uri"].Value));
                    targ.Disconnected += delegate(object sender, EventArgs e)
                    {
                        Console.WriteLine("Connection error, waiting 5 seconds before reconnecting...");
                        System.Threading.Thread.Sleep(5000);
                        //((RestreamTarget)sender).Stop();
                        ((RestreamTarget)sender).Start();
                    };
                    targets.Add(targ);
                }
            }

            foreach (var receiver in receivers)
            {
                Console.WriteLine("Starting receiver: {0}", receiver.SourceUri);
                receiver.Start();
            }
            foreach (var target in targets)
            {
                Console.WriteLine("Starting broadcaster: {0}:{1} (Type: {2}, Mountpoint: {3})", target.ServerHost, target.ServerPort, target.Type, target.Mountpoint);
                target.Start();
            }

            Console.WriteLine("Initialization finished.");
            Console.TreatControlCAsInput = true;
            while (true)
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.C && key.Modifiers == ConsoleModifiers.Control)
                    break;
            }

            foreach (var target in targets)
            {
                Console.WriteLine("Stopping broadcaster: {0}:{1} (Type: {2}, Mountpoint: {3})", target.ServerHost, target.ServerPort, target.Type, target.Mountpoint);
                target.Stop();
            }
            foreach (var receiver in receivers)
            {
                Console.WriteLine("Stopping receiver: {0}", receiver.SourceUri);
                receiver.Stop();
            }

            return 0;
        }
    }
}
