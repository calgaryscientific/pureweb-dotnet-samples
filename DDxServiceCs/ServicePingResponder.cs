//
// Copyright (c) 2012 Calgary Scientific Inc., all rights reserved.
//

/* These several classes allow us to conduct various times of Pings in DDx. There are traditional pings which travel 
 * between the Client to the Service (via the Server) but there are also those which travel from the Service to the Server. 
 */
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using PureWeb.Server;
using log4net;
using System.Diagnostics;

namespace DDxServiceCs
{

    class ServicePing
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ServicePing));

        public delegate void PingResponseHandler(double pingTimeInMilliseconds);

        private PingResponseHandler pingResponseHandler;
        private StateManager stateManager;

        //Ping Fields
        private Stopwatch stopwatch; 

        public ServicePing(StateManager stm, PingResponseHandler hnd)
        {
            this.pingResponseHandler = hnd;
            this.stateManager = stm;
            this.stopwatch = new Stopwatch(); 

    
            stateManager.SystemMessageDispatcher.RegisterSystemMessageHandler("DDx-Pong", DDxPongHandler);
         }

        private void DDxPongHandler(XElement message)
        {
            stopwatch.Stop();
            pingResponseHandler(stopwatch.Elapsed.TotalMilliseconds);
            stateManager.SystemMessageDispatcher.UnregisterSystemMessageHandler("DDx-Pong", DDxPongHandler);
            Reset();
        }

        private void Reset()
        {
            stopwatch.Reset(); 
        }


        public void SendPing()
        {
            stopwatch.Start();
            stateManager.SystemMessageDispatcher.QueueSystemMessage("DDx-Ping", new XElement("EmptyPingMessage"));
        }
    }

    class PingResponder : IStateManagerPlugin
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(PingResponder));

        private StateManager m_stateManager;
        private int pingResponsesReceivedCount = 0;

        public PingResponder()
        {
            m_stateManager = null;
        }

        public void Initialize(StateManager pStateManager)
        {
            m_stateManager = pStateManager;

            //Reply to a Ping from a DDx Client 
            m_stateManager.CommandManager.AddUiHandler("DDxRoundtripPing", ClientPingReply);

            //Trigger a Service to Server Ping
            m_stateManager.CommandManager.AddUiHandler("DDxServiceServerPing", TriggerServerPing);
        }

        public void Uninitialize()
        {
        }

        public void SessionConnected(Guid sessionId, XElement command)
        {
        }

        public void SessionDisconnected(Guid sessionId, XElement command)
        {
        }

        void ClientPingReply(Guid sessionId, XElement command, XElement responses)
        {
            responses.Add(new XElement("DDxClientPingResponse", ""));
        }


        void TriggerServerPing(Guid sessionId, XElement command, XElement responses)
        {
            pingResponsesReceivedCount++; 

            ServicePing servicePing = new ServicePing(m_stateManager, delegate(double pingTimeInMilliseconds)
            {
                m_stateManager.XmlStateManager.SetValue("DDxServiceServerPingResponseCount", pingResponsesReceivedCount);
                m_stateManager.XmlStateManager.SetValue("DDxServiceServerPingResponse", pingTimeInMilliseconds);
            });

            servicePing.SendPing(); 
        }
    }
}
