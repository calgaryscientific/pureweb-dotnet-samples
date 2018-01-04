//
// Copyright (c) 2012 Calgary Scientific Inc., all rights reserved.
//

using System;
using System.Drawing;
using log4net;
using PureWeb;
using PureWeb.Server;
using PureWeb.Ui;

namespace DDxServiceCs
{
    class DDxOwnershipView : IRenderedView
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (DDxOwnershipView));

        public String m_viewName;
        private Size m_clientSize;
        private IRemoteRenderer m_remoteRenderer;

        public DDxOwnershipView(StateManager stateManager)
        {
            m_viewName = "DDx_OwnershipView";
            m_remoteRenderer = stateManager.ViewManager;
            stateManager.ViewManager.RegisterView(m_viewName, this);
        }

        public virtual void SetClientSize(Size clientSize)
        {
            if (clientSize != m_clientSize)
            {
                m_clientSize = clientSize;
            }
        }

        public virtual Size GetActualSize()
        {
            if (m_clientSize.IsEmpty)
                return DDx.DefaultSize;

            return m_clientSize; 
        }

        public bool RequiresRender()
        {
            return false;
        }

        public virtual void RenderView(RenderTarget target)
        {
                var image = target.Image;

                Canvas c = new Canvas(ref image);
                c.Clear(PureWebColor.FromKnownColor(PureWebKnownColor.DarkBlue));
                for (int i = 0; i < image.Height; i = i + 20)
                {
                    c.DrawHLine(PureWebColor.FromKnownColor(PureWebKnownColor.AntiqueWhite), 2, 0, image.Width, i);
                }
        }

        public void PostMouseEvent(PureWebMouseEventArgs mouseEvent)
        {
        }
       
        public void PostKeyEvent(PureWebKeyboardEventArgs keyEvent)
        {
        }

        public void RenderDeferred()
        {
            m_remoteRenderer.RenderViewDeferred(m_viewName);
        }

    }
}
