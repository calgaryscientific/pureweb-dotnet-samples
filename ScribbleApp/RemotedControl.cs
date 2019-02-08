// Copyright 2013-2019 Calgary Scientific Inc. (operating under the brand name of PureWeb)
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
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PureWeb.Server;
using PureWeb.Ui;
using Trace = PureWeb.Diagnostics.Trace;

namespace ScribbleApp
{
    /// <summary>
    /// A UserControl that implements remote rendering through 
    /// the PureWeb IRenderedView and IRemoteRenderer interfaces.
    /// </summary>
    public class RemotedControl : UserControl, IRenderedView
    {
        #region Fields

        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;
        const int WM_SYSKEYDOWN = 0x104;
        const int WM_SYSKEYUP = 0x105;

        IRemoteRenderer m_remoteRenderer;
        string m_viewName;
        bool m_canDeferRendering = true;
        bool m_hasPendingRemoteRender;

        #endregion

        #region Private Methods

        /// <summary>
        /// PostMessage P/Invoke.
        /// </summary>
        /// <returns></returns>
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// SendMessage P/Invoke.
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!m_hasPendingRemoteRender)
            {
                m_hasPendingRemoteRender = true;
                Action action = () =>
                {
                    if (CanDeferRendering)
                        RemoteRender();
                    else
                        RemoteRenderImmediate();

                    m_hasPendingRemoteRender = false;
                };
                this.BeginInvoke(action);
            }
            base.OnPaint(e);
        }

        #endregion

        #region IRenderedView Members

        public virtual void SetClientSize(System.Drawing.Size clientSize)
        {
            if (this.ClientSize != clientSize)
            {
                this.ClientSize = clientSize;
                this.Invalidate();
            }
        }

        public virtual System.Drawing.Size GetActualSize()
        {
            return this.ClientSize;
        }

        public bool RequiresRender()
        {
            return false;
        }

        public virtual void RenderView(RenderTarget target)
        {
            var image = target.Image;
            try
            {
                var startTime = System.DateTime.Now;
                m_hasPendingRemoteRender = true;

                if (m_offscreen != null)
                {
                    image.DrawUnscaled(m_offscreen);
                }

                var endTime = System.DateTime.Now;
                Trace.WriteLine("RenderView: " + (endTime - startTime).Milliseconds + " ms");
            }
            finally
            {
                m_hasPendingRemoteRender = false;
            }
        }

        private Bitmap m_offscreen;
        public void SetOffScreen(Bitmap b)
        {
            if (b != m_offscreen)
            {
                m_offscreen = b;
                RemoteRender();
            }
        }

        public void PostKeyEvent(PureWebKeyboardEventArgs keyEvent)
        {
            Program.StateManager.XmlStateManager[ViewName + "/KeyEvent/Type"] = keyEvent.EventType.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/KeyEvent/CharacterCode"] = keyEvent.CharacterCode.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/KeyEvent/KeyCode"] = keyEvent.KeyCode.ToString();

            bool isAltDown = 0 != (keyEvent.Modifiers & Modifiers.Alternate);

            int wParam = (int)keyEvent.KeyCode;
            int lParam = isAltDown ? (1 << 29) : 0; // "context code";
            int message;

            if (isAltDown || keyEvent.KeyCode == KeyCode.F10)
            {
                message = keyEvent.EventType == KeyboardEventType.KeyDown ? WM_SYSKEYDOWN : WM_SYSKEYUP;
            }
            else
            {
                message = keyEvent.EventType == KeyboardEventType.KeyDown ? WM_KEYDOWN : WM_KEYUP;
            }

            PostMessage(this.Handle, (UInt32)message, new IntPtr(wParam), new IntPtr(lParam));
        }

        public void PostMouseEvent(PureWebMouseEventArgs mouseEvent)
        {
            Program.StateManager.XmlStateManager[ViewName + "/MouseEvent/Type"] = mouseEvent.EventType.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/MouseEvent/ChangedButton"] = mouseEvent.ChangedButton.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/MouseEvent/Buttons"] = mouseEvent.Buttons.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/MouseEvent/Modifiers"] = mouseEvent.Modifiers.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/MouseEvent/X"] = mouseEvent.X.ToString();
            Program.StateManager.XmlStateManager[ViewName + "/MouseEvent/Y"] = mouseEvent.Y.ToString();

            Trace.WriteLine("Post {0}: x:{1} y:{2} left:{3}", mouseEvent.EventType, mouseEvent.X, mouseEvent.Y, (mouseEvent.Buttons & PureWeb.Ui.MouseButtons.Left) != 0 ? "y" : "n");

            System.Windows.Forms.MouseButtons buttons = System.Windows.Forms.MouseButtons.None;
            if (0 != (mouseEvent.Buttons & PureWeb.Ui.MouseButtons.Left)) buttons |= System.Windows.Forms.MouseButtons.Left;
            if (0 != (mouseEvent.Buttons & PureWeb.Ui.MouseButtons.Right)) buttons |= System.Windows.Forms.MouseButtons.Right;
            if (0 != (mouseEvent.Buttons & PureWeb.Ui.MouseButtons.Middle)) buttons |= System.Windows.Forms.MouseButtons.Middle;
            if (0 != (mouseEvent.Buttons & PureWeb.Ui.MouseButtons.XButton1)) buttons |= System.Windows.Forms.MouseButtons.XButton1;
            if (0 != (mouseEvent.Buttons & PureWeb.Ui.MouseButtons.XButton2)) buttons |= System.Windows.Forms.MouseButtons.XButton2;

            System.Windows.Forms.MouseButtons changed = System.Windows.Forms.MouseButtons.None;
            if (0 != (mouseEvent.ChangedButton & PureWeb.Ui.MouseButtons.Left)) changed |= System.Windows.Forms.MouseButtons.Left;
            if (0 != (mouseEvent.ChangedButton & PureWeb.Ui.MouseButtons.Right)) changed |= System.Windows.Forms.MouseButtons.Right;
            if (0 != (mouseEvent.ChangedButton & PureWeb.Ui.MouseButtons.Middle)) changed |= System.Windows.Forms.MouseButtons.Middle;
            if (0 != (mouseEvent.ChangedButton & PureWeb.Ui.MouseButtons.XButton1)) changed |= System.Windows.Forms.MouseButtons.XButton1;
            if (0 != (mouseEvent.ChangedButton & PureWeb.Ui.MouseButtons.XButton2)) changed |= System.Windows.Forms.MouseButtons.XButton2;

            switch (mouseEvent.EventType)
            {
                case MouseEventType.MouseEnter:
                    OnMouseEnter(EventArgs.Empty);
                    break;

                case MouseEventType.MouseLeave:
                    OnMouseLeave(EventArgs.Empty);
                    break;

                case MouseEventType.MouseMove:
                    OnMouseMove(new MouseEventArgs(buttons, 0, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                    break;

                case MouseEventType.MouseDown:
                    OnMouseDown(new MouseEventArgs(buttons, 0, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                    break;

                case MouseEventType.MouseUp:
                    OnMouseUp(new MouseEventArgs(changed, 0, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                    if (GetStyle(ControlStyles.StandardClick))
                    {
                        OnClick(new MouseEventArgs(changed, 1, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                        OnMouseClick(new MouseEventArgs(changed, 1, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                    }
                    break;

                case MouseEventType.MouseDoubleClick:
                    if (GetStyle(ControlStyles.StandardClick) || GetStyle(ControlStyles.StandardDoubleClick))
                    {
                        OnDoubleClick(new MouseEventArgs(buttons, 2, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                        OnMouseDoubleClick(new MouseEventArgs(buttons, 2, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                    }
                    break;

                case MouseEventType.MouseWheel:
                    OnMouseWheel(new MouseEventArgs(buttons, 0, (int)mouseEvent.X, (int)mouseEvent.Y, (int)mouseEvent.Delta));
                    break;

                default:
                    Trace.WriteLine("Received unknown mouse event type {0}.", (int)mouseEvent.EventType);
                    return;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets if the control is in design mode, or if any of its
        /// parents are in design mode.
        /// </summary>
        private bool IsDesignerHosted
        {
            get
            {
                Control ctrl = this;
                while (ctrl != null)
                {
                    if (ctrl.Site == null)
                        return false;
                    if (ctrl.Site.DesignMode == true)
                        return true;
                    ctrl = ctrl.Parent;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets or sets the name of the view that being remotely rendered to.
        /// </summary>
        /// <value>The name of the view.</value>
        [DefaultValue("View")]
        public string ViewName
        {
            get { return m_viewName; }
            set
            {
                m_viewName = value;
            }
        }

        /// <summary>
        /// Gets or sets the remote renderer.
        /// </summary>
        /// <value>The remote renderer.</value>
        [Browsable(false)]
        public IRemoteRenderer RemoteRenderer
        {
            get { return m_remoteRenderer; }
            set
            {
                m_remoteRenderer = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can support deferred rendering.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance can support deferred rendering; otherwise, <c>false</c>.
        /// </value>
        [Browsable(false)]
        public bool CanDeferRendering
        {
            get { return m_canDeferRendering; }
            set { m_canDeferRendering = value; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Causes a remote render of the current contents of the control.  
        /// The actual callback to render may be deferred.
        /// </summary>
        public virtual void RemoteRender()
        {
            if (m_remoteRenderer == null)
                return;

            m_remoteRenderer.RenderViewDeferred(m_viewName);
        }

        /// <summary>
        /// Causes a remote render of the current contents of the control.  
        /// The callback to render is performed before the call returns.
        /// </summary>
        public void RemoteRenderImmediate()
        {
            if (m_remoteRenderer == null)
                return;

            m_remoteRenderer.RenderViewImmediate(m_viewName);
        }

        #endregion
    }
}