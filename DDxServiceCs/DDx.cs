﻿// Copyright 2013-2019 Calgary Scientific Inc. (operating under the brand name of PureWeb)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Net;
using PureWeb.Server;
using PureWeb.Xml;
using log4net;
using System.Xml;
using System.Xml.Linq;
using System.Drawing;
using System.Text;

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
            m_stateManager = new StateManager(new PureWeb.Server.WindowsDispatcher());
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
            m_stateManager.CommandManager.AddUiHandler("RotateDDxViewBkColors", OnRotateDDxViewBkColors);
            m_stateManager.CommandManager.AddUiHandler("SessionStorageBroadcast", OnSessionStorageBroadcast);
            m_stateManager.CommandManager.AddUiHandler("AttachStorageListener", OnAttachStorageListener);
            m_stateManager.CommandManager.AddUiHandler("DetachStorageListener", OnDetachStorageListener);
            m_stateManager.CommandManager.AddUiHandler("QuerySessionStorageKeys", OnQuerySessionStorageKeys);
            m_stateManager.CommandManager.AddUiHandler("QuerySessionsWithKey", OnQuerySessionsWithKey);
            m_stateManager.CommandManager.AddUiHandler("SessionStorageSetKeyForceResponse", OnSessionStorageSetKeyForceResponse);
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
            m_stateManager.CommandManager.RemoveUiHandler("RotateDDxViewBkColors");
            m_stateManager.CommandManager.RemoveUiHandler("SessionStorageBroadcast");
            m_stateManager.CommandManager.RemoveUiHandler("AttachStorageListener");
            m_stateManager.CommandManager.RemoveUiHandler("DetachStorageListener");
            m_stateManager.CommandManager.RemoveUiHandler("QuerySessionStorageKeys");
            m_stateManager.CommandManager.RemoveUiHandler("QuerySessionsWithKey");
            m_stateManager.CommandManager.RemoveUiHandler("SessionStorageSetKeyForceResponse");

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

        private void OnRotateDDxViewBkColors(Guid sessionId, XElement command, XElement responses)
        {
            Logger.Debug("Unregistering DDx Views");

            StateManager stateManager = PureWeb.Server.StateManager.Instance;

            for (int i = 0; i < m_viewCount; i++)
            {
                stateManager.ViewManager.UnregisterView(m_views[i].m_viewName);
            }

            Logger.Debug("Rotating DDx Views background colors");

            // rotate the view background colors - this has the effect of a clockwise rotation
            // (mod 4) of the view background colors in the HTML5 client so the first time this
            // command is invoked we get:
            //
            // |-------------------|         |--------------------|
            // | black  | blue     |         | magenta | black    |
            // |-------------------| goes to |--------------------|
            // | green  | magenta  |         | blue    | green    |
            // |-------------------|         |--------------------|

            int bkColorIndex = m_views[m_viewCount - 1].BkColorIndex;
            for (int i = m_viewCount - 1; i > 0; i--)
            {
                m_views[i].BkColorIndex = m_views[i - 1].BkColorIndex;
            }
            m_views[0].BkColorIndex = bkColorIndex;

            Logger.Debug("Re-registering DDx Views");

            for (int i = 0; i < m_viewCount; i++)
            {
                stateManager.ViewManager.RegisterView(m_views[i].m_viewName, m_views[i]);
                m_views[i].RenderDeferred();
            }
        }

        private void OnSessionStorageValueChanged(Object source, SessionStorageChangedEventArgs args)
        {
            if (args.ChangeType == SessionStorageChangeType.Set)
            {
                var key = "ServiceListenerReverser-" + args.Key;
                m_stateManager.SessionStorageManager.SetValue(args.SessionId, key, Reverse(args.NewValue));
            }
        }

        // reversing a string in C# turns out to be tricky if one wants to support characters
        // encoded as UTF-16 surrogate pairs - cant swap the order of them without ending up
        // with invalid Unicode. The following is adapted from:
        // http://stackoverflow.com/questions/228038/best-way-to-reverse-a-string#228460

        private string Reverse(string input)
        {
            var output = new char[input.Length];
            for (int outputIndex = 0, inputIndex = input.Length - 1;
                outputIndex < input.Length;
                outputIndex++, inputIndex--)
            {
                // check for surrogate pair
                if (input[inputIndex] >= 0xDC00 &&
                    input[inputIndex] <= 0xDFFF &&
                    inputIndex > 0 &&
                    input[inputIndex - 1] >= 0xD800 &&
                    input[inputIndex - 1] <= 0xDBFF)
                {
                    // preserve the order of the surrogate pair code units
                    output[outputIndex + 1] = input[inputIndex];
                    output[outputIndex] = input[inputIndex - 1];
                    outputIndex++;
                    inputIndex--;
                }
                else
                {
                    output[outputIndex] = input[inputIndex];
                }
            }

            return new string(output);
        }

        private void OnSessionStorageBroadcast(Guid sessionId, XElement command, XElement responses)
        {
            var key = command.GetText("/key");
            var value = command.GetText("/value");
            m_stateManager.SessionStorageManager.SetValueForAllSessions(key, value);
        }

        private void OnAttachStorageListener(Guid sessionId, XElement command, XElement responses)
        {
            var key = command.GetText("/key");
            m_stateManager.SessionStorageManager.AddValueChangedHandler(sessionId, key, OnSessionStorageValueChanged);
        }

        private void OnDetachStorageListener(Guid sessionId, XElement command, XElement responses)
        {
            var key = command.GetText("/key");
            m_stateManager.SessionStorageManager.RemoveValueChangedHandler(sessionId, key, OnSessionStorageValueChanged);
        }

        private void OnQuerySessionStorageKeys(Guid sessionId, XElement command, XElement responses)
        {
            var keys = m_stateManager.SessionStorageManager.GetKeys(sessionId);
            var keyStr ="";

            foreach (var key in keys)
                keyStr += key + ";";
            
            responses.SetText("/keys", keyStr);
        }

        private void OnQuerySessionsWithKey(Guid sessionId, XElement command, XElement responses)
        {
            var key = command.GetText("/key");
            var sessionIds = m_stateManager.SessionStorageManager.GetSessionsContainingKey(key);
            var keyStr ="";

            foreach (var id in sessionIds)
                keyStr += id.ToString() + ";";
            
            responses.SetText("/guids", keyStr);
        }

        private void OnSessionStorageSetKeyForceResponse(Guid sessionId, XElement command, XElement response)
        {
            var key = command.GetText("/key");
            var value = command.GetText("/value");
            m_stateManager.SessionStorageManager.SetValue(sessionId, key, value, true);
        }
    }
}
