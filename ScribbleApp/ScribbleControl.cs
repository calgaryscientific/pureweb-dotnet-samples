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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using PureWeb.Xml;

namespace ScribbleApp
{
    public partial class ScribbleControl : RemotedControl
    {
        const float PenWidth = 2.0f;

        private List<Stroke> m_strokes = new List<Stroke>();
        private Stroke m_currentStroke = null;
        private Pen m_pen = new Pen(Color.White, PenWidth);
        private bool m_isRegistered = false;
        private Bitmap m_offscreen;

        public ScribbleControl()
        {
            InitializeComponent();

        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);

            // unregister with the state maanger
            if (Program.StateManager != null)
            {
                if (m_isRegistered)
                {
                    this.RemoteRenderer = null;
                    Program.StateManager.ViewManager.UnregisterView(this.ViewName);
                    Program.StateManager.XmlStateManager.RemoveValueChangedHandler("ScribbleColor", OnScribbleColorChanged);
                    Program.StateManager.CommandManager.RemoveUiHandler("Clear");
                }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // register with the state manager
            if (Program.StateManager != null)
            {
                this.RemoteRenderer = Program.StateManager.ViewManager;
                var vo = new PureWeb.Server.ViewRegistrationOptions(true, true);
                Program.StateManager.ViewManager.RegisterView(this.ViewName, this, vo);
                Program.StateManager.XmlStateManager.AddValueChangedHandler("ScribbleColor", OnScribbleColorChanged);
                Program.StateManager.CommandManager.AddUiHandler("Clear", OnExecuteClear);
                m_isRegistered = true;

                Program.StateManager.XmlStateManager["ScribbleColor"] = m_pen.Color.Name;
            }
        }

        private void OnExecuteClear(Guid sessionId, XElement command, XElement responses)
        {
            ClearStrokes();
            Invalidate();
        }

        private void OnScribbleColorChanged(object Sender, ValueChangedEventArgs args)
        {
            Color newColor = Color.FromName(args.NewValue);
            if (!newColor.IsKnownColor)
            {
                // reset app state to old pen color name
                Program.StateManager.XmlStateManager["ScribbleColor"] = m_pen.Color.Name;
                return;
            }

            m_pen = new Pen(newColor, PenWidth);
            if (m_offscreen != null)
            {
                PaintStrokes(Graphics.FromImage(m_offscreen), this.Size);
            }

            Invalidate();
        }

        private void EndStroke()
        {
            m_currentStroke = null;
        }

        private void BeginStroke()
        {
            EndStroke();
            m_currentStroke = new Stroke();
            m_strokes.Add(m_currentStroke);
        }

        private void ClearStrokes()
        {
            EndStroke();
            m_strokes.Clear();
            if (m_offscreen != null)
            {
                Graphics.FromImage(m_offscreen).Clear(Color.Black);
            }
        }

        private void DrawCurrentStroke()
        {
            if (m_currentStroke != null)
            {
                using (Graphics graphics = this.CreateGraphics())
                {
                    m_currentStroke.Draw(graphics, m_pen);
                    if (m_offscreen != null)
                    {
                        m_currentStroke.Draw(Graphics.FromImage(m_offscreen), m_pen);
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintStrokes(e.Graphics, this.Size);
        }

        private void PaintStrokes(Graphics graphics, Size size)
        {
            foreach (Stroke stroke in m_strokes)
            {
                stroke.Draw(graphics, m_pen);
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == 'c')
            {
                ClearStrokes();
                Invalidate();
            }

            base.OnKeyPress(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                if (m_currentStroke == null)
                    BeginStroke();

                m_currentStroke.Add(e.Location);
                DrawCurrentStroke();
                RemoteRender();
            }
            else
            {
                if (m_currentStroke != null)
                {
                    DrawCurrentStroke();
                    RemoteRender();
                    EndStroke();
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                // Tell the StateManager we are interacting with this view
                Program.StateManager.ViewManager.SetViewInteracting(ViewName, true);
                Capture = true;

                BeginStroke();
                m_currentStroke.Add(e.Location);
                DrawCurrentStroke();
                RemoteRender();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                // Tell the StateManager we are finished interacting with this view
                Program.StateManager.ViewManager.SetViewInteracting(ViewName, false);
                Capture = false;
                
                if (m_currentStroke != null)
                    m_currentStroke.Add(e.Location);

                DrawCurrentStroke();
                RemoteRender();
                EndStroke();
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ClearStrokes();
                Invalidate();
            }

            base.OnMouseDoubleClick(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            m_offscreen = new Bitmap(this.Width, this.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);            
            PaintStrokes(Graphics.FromImage(m_offscreen), this.Size);

            SetOffScreen(m_offscreen);
        }
    }

    internal class Stroke
    {
        private List<PointF> m_points = new List<PointF>();
        private PointF[] m_cachedPoints = null;

        public void Add(Point point)
        {
            m_points.Add(new PointF(point.X, point.Y));
            m_cachedPoints = null;
        }

        public void Add(PointF point)
        {
            m_points.Add(point);
            m_cachedPoints = null;
        }

        public void Draw(Graphics g, Pen pen)
        {
            if (m_cachedPoints == null)
                m_cachedPoints = m_points.ToArray();

            if (m_cachedPoints.Length == 1)
            {
                g.DrawLine(Pens.Black, m_cachedPoints[0], m_cachedPoints[0]);
            }
            else if (m_cachedPoints.Length > 1)
            {
                g.DrawLines(pen, m_cachedPoints);
            }
        }
    }
}
