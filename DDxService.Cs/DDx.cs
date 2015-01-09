//
// Copyright (c) 2012 Calgary Scientific Inc., all rights reserved.
//
using System;
using System.Diagnostics;
using System.Net;
using PureWeb.Server;
using PureWeb.Xml;
using log4net;
using System.Xml;
using System.Xml.Linq;
using System.Drawing;
using System.Collections.Generic;
using PureWeb.Ui;

namespace DDxServiceCs
{
    internal class DDx : ISessionDefaultColorProvider
    {
        public static Size DefaultSize = new Size(1400, 900);

        public const String _DDx = "/DDx";
        public const String _DDx_ASYNCIMAGEGENERATION = _DDx + "/AsyncImageGeneration";
        public const String _DDx_RAPIDIMAGEGENERATION = _DDx + "/RapidImageGeneration";
        public const String _DDx_USEDEFERREDRENDERING = _DDx + "/UseDeferredRendering";
        public const String _DDx_USECLIENTSIZE = _DDx + "/UseClientSize";
        public const String _DDx_USETRY = "/DDx/UseClientSize";
        public const String _DDx_USETILES = _DDx + "/UseTiles";
        public const String _DDx_SHOWMOUSEPOS = _DDx + "/ShowMousePos";
        public const String _DDx_GRID = _DDx + "/Grid";
        public const String _DDx_GRID_ON = _DDx_GRID + "/On";
        public const String _DDx_GRID_MARGIN = _DDx_GRID + "/Margin";
        public const String _DDx_GRID_LINESPACING = _DDx_GRID + "/LineSpacing";
        public const String _DDx_GRID_LINEWIDTH = _DDx_GRID + "/LineWidth";

        private static readonly ILog Logger = LogManager.GetLogger(typeof (DDx));

        private static int m_colorCount = 0;
        private const int m_viewCount = 4;

        private DDxView[] m_views =  new DDxView[m_viewCount];
        private PGView m_pgView;
        private DDxOwnershipView m_ownershipView;

        private PingResponder m_pingResponder;
        private IPAddress m_address;
        private int m_port;
        private static StateManagerServer m_stateManagerServer;
        private static StateManager m_stateManager;

        public void Go(string[] args)
        {
            m_stateManager = new StateManager(System.Windows.Threading.Dispatcher.CurrentDispatcher);
            m_stateManagerServer = new StateManagerServer();

            m_stateManager.Initialized += new EventHandler(OnPureWebStartup);
            m_stateManager.Uninitialized += new EventHandler(OnPureWebShutdown);
            if (args.Length == 0)
            {
                m_stateManagerServer.Start(m_stateManager);
            }
            else
            {
                m_port = 8082;
                if (args.Length > 1)
                {
                    m_port = int.Parse(args[1]);
                }
                m_address = IPAddress.Parse(args[0]);
                m_stateManagerServer.Start(m_stateManager, m_address, m_port);
            }


            System.Windows.Threading.Dispatcher.Run();
        }
        
        private void OnPureWebStartup(object sender, EventArgs args)
        {
            // Initialize PW state properties
            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                state.SetValue(_DDx_GRID_ON, true);
                state.SetValue(_DDx_GRID_MARGIN, 0);
                state.SetValue(_DDx_GRID_LINEWIDTH, 4);
                state.SetValue(_DDx_GRID_LINESPACING, 100);
            }

            // Register PW Handlers
            m_stateManager.CommandManager.AddUiHandler("TakeOwnership", OnTakeOwnership);
            m_stateManager.CommandManager.AddUiHandler("SetProperty", OnSetProperty);
            m_stateManager.CommandManager.AddUiHandler("Echo", OnEcho);
            m_stateManager.CommandManager.AddIoHandler("TestMerge", OnTestMerge);
            m_stateManager.XmlStateManager.AddChildChangedHandler(_DDx_GRID, OnGridStateChanged);
            m_stateManager.XmlStateManager.AddChildChangedHandler("/PureWeb/Profiler", OnProfilerStateChanged);

            CollaborationManager.Instance.SessionDefaultColorProvider = this;

            // Let the views register themselves
            for (int i = 0; i < m_viewCount; i++)
            {
                m_views[i] = new DDxView(i, m_stateManager);
            }
            m_pgView = new PGView(m_stateManager);
            m_ownershipView = new DDxOwnershipView(m_stateManager);
            m_pingResponder = new PingResponder();
            StateManager.PluginManager.RegisterPlugin("DDxPingResponder", m_pingResponder);
        }

        /// <summary>
        /// Perform a timing test on merging large application state.
        /// </summary>
        /// <param name="sessionid"></param>
        /// <param name="command"></param>
        /// <param name="responses"></param>
        private void OnTestMerge(Guid sessionid, XElement command, XElement responses)
        {
            var root = new XElement("Root");
            var a = new XElement("A");
            a.SetOrderedChildName("B");
            root.Add(a);

            var diffScript = new XElement("DiffScript");
            diffScript.SetOrderedChildName("Diff");

            int count = command.GetTextAs("Count", 3000);
            for (int i = 0; i < count; i++)
            {
                var nodeStr = string.Format("<Diff><Type>Inserted</Type><Path>/A/#{0}</Path><Value /></Diff>", i);
                var node = XElement.Parse(nodeStr);
                diffScript.Add(node);
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            XmlDifference.MergeInPlace(root, diffScript, null);
            m_stateManager.XmlStateManager.SetValue("/OnTestMerge", sw.ElapsedMilliseconds);
        }

        private void OnTakeOwnership(Guid sessionId, XElement command, XElement responses)
        {
            CollaborationManager.Instance.OwnerSession = sessionId;
        }

        private void OnPureWebShutdown(object sender, EventArgs args)
        {
            for (int i = 0; i < m_viewCount; i++)
            {
                m_stateManager.ViewManager.UnregisterView(m_views[i].m_viewName);
            }
            m_pgView.Uninitialize();
            m_stateManager.ViewManager.UnregisterView(m_pgView.m_viewName);
            m_stateManager.ViewManager.UnregisterView(m_ownershipView.m_viewName);

            m_stateManager.XmlStateManager.RemoveAllValueChangedHandlers(_DDx_USETILES);

            m_stateManager.CommandManager.RemoveUiHandler("TakeOwnership");
            m_stateManager.CommandManager.RemoveUiHandler("SetProperty");
            m_stateManager.CommandManager.RemoveUiHandler("Echo");
            m_stateManager.CommandManager.RemoveUiHandler("TestMerge");

            m_colorCount = 0; // default color provider reset
            DDxView.m_colorCount = 0; // reset DDxView colors

            StateManager.PluginManager.UnregisterPlugin("DDxPingResponder", m_pingResponder);

            // if running unmanaged, restart the state manager server to connect back to the server

            if (m_address != null)
            {
                try
                {
                    m_stateManagerServer.Start(m_stateManager, m_address, m_port);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred starting the StateManagerServer: " + e.Message);
                }
            }
            else
            {
                Environment.Exit(0);
            }
        }

        public PureWeb.PureWebColor GetSessionDefaultColor(Guid sessionId)
        {
            switch (m_colorCount++%7)
            {
                case 0:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Red);
                case 1:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Orange);
                case 2:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Yellow);
                case 3:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Green);
                case 4:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Blue);
                case 5:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Indigo);
                case 6:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.Violet);
                default:
                    return PureWeb.PureWebColor.FromKnownColor(PureWeb.PureWebKnownColor.White);
            }
        }

        private void OnGridStateChanged(object sender, ValueChangedEventArgs args)
        {
            for (int i = 0; i < m_viewCount; i++)
            {
                m_views[i].RenderDeferred();
            }
        }

        private void OnProfilerStateChanged(object sender, ValueChangedEventArgs args)
        {
            if (!args.Path.Contains("LastUpdated"))
                return;

            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = false;
                settings.NewLineChars = " ";
                settings.NewLineHandling = NewLineHandling.Replace;
                Logger.InfoFormat("Profiler info updated: {0}",
                                  String.Format(state.GetValue("/PureWeb/Profiler"), false, settings));
            }
        }

        private void OnSetProperty(Guid sessionId, XElement command, XElement responses)
        {
            String path = command.GetText("Path");
            String value = command.GetText("Value");

            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                state.SetValue(path, value);
                Logger.DebugFormat("SetProperty {0} {1}", path, value);
            }
        }

        private void OnEcho(Guid sessionId, XElement command, XElement responses)
        {
            var key = command.GetText("Key");
            var contentType = command.GetText("Type");
            object content = null;

            if (contentType.Equals("Text", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetText("Content");
            }
            else if (contentType.Equals("Character", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<Char>("Content");
            }
            else if (contentType.Equals("DateTime", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<DateTime>("Content");
            }
            else if (contentType.Equals("Byte", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<byte>("Content");
            }
            else if (contentType.Equals("Integer", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<int>("Content");
            }
            else if (contentType.Equals("Unsigned Integer", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<uint>("Content");
            }
            else if (contentType.Equals("Long", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<long>("Content");
            }
            else if (contentType.Equals("Unsigned Long", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<ulong>("Content");
            }
            else if (contentType.Equals("Float", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<float>("Content");
            }
            else if (contentType.Equals("Double", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<double>("Content");
            }
            else if (contentType.Equals("Decimal", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<decimal>("Content");
            }
            else if (contentType.Equals("Boolean", StringComparison.InvariantCultureIgnoreCase))
            {
                content = command.GetTextAs<bool>("Content");
            }

            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                string path = string.Format("/DDx/Echo/{0}", key);
                state.SetValue(path, content);
            }

            responses.Add(new XElement("Key", key));
            responses.Add(new XElement("Content", content));
        }
    }
}
