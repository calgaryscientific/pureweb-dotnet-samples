//
// Copyright (c) 2012 Calgary Scientific Inc., all rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PureWeb.Xml;
using log4net;
using PureWeb.Server;
using PureWeb.Ui;
using PureWeb;
using Image = PureWeb.Server.Image;

namespace DDxServiceCs
{
    internal class PGView : IRenderedView
    {
        private const PixelFormat PIXEL_FORMAT = PixelFormat.Format32bppArgb;
        private static readonly ILog Logger = LogManager.GetLogger(typeof (PGView));
        private static Size DefaultSize = new Size(800, 900);

        public String m_viewName;
        private readonly Timer m_threadTimer;
        private readonly TimerCallback tcb;

        private enum ImageGenerateState
        {
            Uninitialized,
            Generating,
            Complete
        };

        private bool m_useClientSize = true;
        private bool m_useDeferredRendering = true;
        private bool m_asyncImageGeneration = false;
        private bool m_rapidImageGeneration = false;
        private bool m_showMousePos = true;

        private int m_mouseX;
        private int m_mouseY;
        private int m_currentImage;

        private CancellationTokenSource cts;

        private Size m_clientSize = new Size(DefaultSize.Width, DefaultSize.Height);
        private Size m_imgGenerationSize;
        private ImageGenerateState m_imgGenerationState = ImageGenerateState.Uninitialized; 
        private List<Image> m_imageList = new List<Image>();

        private IRemoteRenderer m_remoteRenderer;

        public PGView(StateManager stateManager)
        {
            m_viewName = "PGView";
            m_remoteRenderer = stateManager.ViewManager;
            stateManager.ViewManager.RegisterView(m_viewName, this, new ViewRegistrationOptions(true, true));
            stateManager.ViewManager.SetViewImageFormat(m_viewName, new ViewImageFormat() { PixelFormat = PIXEL_FORMAT });        

            StateManager.Instance.XmlStateManager.AddValueChangedHandler(DDx._DDx_ASYNCIMAGEGENERATION, OnAsyncImageGenerationChanged);
            StateManager.Instance.XmlStateManager.AddValueChangedHandler(DDx._DDx_RAPIDIMAGEGENERATION, OnRapidImageGenerationChanged);
            StateManager.Instance.XmlStateManager.AddValueChangedHandler(DDx._DDx_USEDEFERREDRENDERING, OnUseDeferredRenderingChanged);
            StateManager.Instance.XmlStateManager.AddValueChangedHandler(DDx._DDx_USECLIENTSIZE, OnUseClientSizeChanged);
            StateManager.Instance.XmlStateManager.AddValueChangedHandler(DDx._DDx_SHOWMOUSEPOS, OnShowMousePosChanged);
            StateManager.Instance.ViewManager.ViewRendered += OnViewRendered;
            StateManager.Instance.CommandManager.AddUiHandler("Screenshot", OnScreenshotRequested);

            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                state.SetValue(DDx._DDx_ASYNCIMAGEGENERATION, m_asyncImageGeneration);
                state.SetValue(DDx._DDx_USEDEFERREDRENDERING, m_useDeferredRendering);
                state.SetValue(DDx._DDx_USECLIENTSIZE, m_useClientSize);
                state.SetValue(DDx._DDx_SHOWMOUSEPOS, m_showMousePos);
            }

            tcb = TimerCallback;
            m_threadTimer = new Timer(tcb, null, 15, 15); // approx 60 fps
        }

        private void OnScreenshotRequested(Guid sessionid, XElement command, XElement responses)
        {
            Image current = null;


            if (!GenerateImages(m_useClientSize?m_clientSize:DefaultSize, out current))
            {
                if (current != null)
                {
                    using(MemoryStream stream = new MemoryStream())
                    {
                        current.Bitmap.Save(stream, ImageFormat.Jpeg);    
                        stream.Flush();

                        Guid key = StateManager.Instance.ResourceManager.Store(new ContentInfo("image/jpeg", stream.ToArray()));
                        responses.Add(new XElement("ResourceKey", key));
                    }
                }
                else
                {
                    throw new Exception("Unable to generate image for screenshot request.");
                }
            }
        }

        public void Uninitialize()
        {
            m_threadTimer.Dispose();
            StateManager.Instance.XmlStateManager.RemoveValueChangedHandler(DDx._DDx_ASYNCIMAGEGENERATION, OnAsyncImageGenerationChanged);
            StateManager.Instance.XmlStateManager.RemoveValueChangedHandler(DDx._DDx_RAPIDIMAGEGENERATION, OnRapidImageGenerationChanged);
            StateManager.Instance.XmlStateManager.RemoveValueChangedHandler(DDx._DDx_USEDEFERREDRENDERING, OnUseDeferredRenderingChanged);
            StateManager.Instance.XmlStateManager.RemoveValueChangedHandler(DDx._DDx_USECLIENTSIZE, OnUseClientSizeChanged);
            StateManager.Instance.XmlStateManager.RemoveValueChangedHandler(DDx._DDx_SHOWMOUSEPOS, OnShowMousePosChanged); 
        }

        void GenerateMasterImage(ref byte[] byteArray, Size masterImageSize, int bytesPerPixel, CancellationToken token)
        {
            const double perRowShift = 10;

            // Loop could be sped up by iterating byteptr per pixel
            for (int y = 0; y < masterImageSize.Height; y++)
            {
                double perPixelIncrement = 512.0 / masterImageSize.Width;
                double pixelValue = (y * perRowShift) % 256;

                for (int x = 0; x < masterImageSize.Width; x++)
                {
                    if (token.IsCancellationRequested) return;
                    var pixelIndex = (y * masterImageSize.Width + x) * bytesPerPixel;

                    byteArray[pixelIndex] = (byte)Convert.ToInt16(pixelValue);
                    byteArray[pixelIndex + 1] = 0;
                    byteArray[pixelIndex + 2] = 0;
                    if (bytesPerPixel == 4) byteArray[pixelIndex + 3] = 0xff;  // Set alpha channel to 0xff

                    pixelValue += perPixelIncrement;

                    if (pixelValue > 255 || pixelValue < 0)
                    {
                        perPixelIncrement *= -1.0;
                        pixelValue += perPixelIncrement;
                    }
                }
            }
        }

        void InitializeImages(CancellationToken token)
        {
            const int imageCount = 25;
            Size imageSize = m_imgGenerationSize;

            // Generate master image, then shift vertically to create the other images.
            Size bufferImageSize = imageSize;
            bufferImageSize.Height += imageCount;
            List<Image> newImages = new List<Image>();

            int bytesPerPixel = Image.GetBytesPerPixel(PIXEL_FORMAT);
            int numBytesBufferImage = bufferImageSize.Width * bufferImageSize.Height * bytesPerPixel;
            byte[] byteArray = new byte[numBytesBufferImage];

            GenerateMasterImage(ref byteArray, bufferImageSize, bytesPerPixel, token);
            if (token.IsCancellationRequested)
                return;

            for (int i = 0; i < imageCount; i++)
            {
                Image image = new Image(imageSize.Width, imageSize.Height, PIXEL_FORMAT);
                BitmapData imageData = image.Bitmap.LockBits(image.Region, ImageLockMode.ReadWrite, image.Bitmap.PixelFormat);

                long destPtr = imageData.Scan0.ToInt64();

                for (int j = 0; j < imageSize.Height; j++, destPtr+= imageData.Stride)
                {
                    if (token.IsCancellationRequested)
                        return;
                    System.Runtime.InteropServices.Marshal.Copy(byteArray, (i + j) * image.Width * bytesPerPixel, new IntPtr(destPtr), image.Width * bytesPerPixel);
                }
                image.Bitmap.UnlockBits(imageData);
                newImages.Add(image);
            }

            m_imageList.Clear();
            m_imageList = newImages;
            m_imgGenerationState = ImageGenerateState.Complete;
            m_remoteRenderer.RenderViewDeferred(m_viewName);
        }

        void AdvanceRendering()
        {
            if (m_imageList.Any())
               m_currentImage = (m_currentImage + 1) % m_imageList.Count();
    
            Render();
        }

        void TimerCallbackUI()
        {
            if (m_asyncImageGeneration)
                AdvanceRendering();
        }

        private void TimerCallback(Object stateInfo)
        {
            UiDispatcher.Invoke(TimerCallbackUI);
        }

        void OnAsyncImageGenerationChanged(object sender, ValueChangedEventArgs args)
        {
            m_asyncImageGeneration = StateManager.Instance.XmlStateManager.GetValueAs<bool>(DDx._DDx_ASYNCIMAGEGENERATION);
            Logger.InfoFormat("AsyncImageGeneration set to {0}", m_asyncImageGeneration ? "true" : "false");
        }

        void OnRapidImageGenerationChanged(object sender, ValueChangedEventArgs args)
        {
            m_rapidImageGeneration = StateManager.Instance.XmlStateManager.GetValueAs<bool>(DDx._DDx_RAPIDIMAGEGENERATION);
            Logger.InfoFormat("RapidImageGeneration set to {0}", m_rapidImageGeneration ? "true" : "false");
        }

        void OnUseDeferredRenderingChanged(object sender, ValueChangedEventArgs args)
        {
            m_useDeferredRendering = StateManager.Instance.XmlStateManager.GetValueAs<bool>(DDx._DDx_USEDEFERREDRENDERING);
            Logger.InfoFormat("DeferredRendering set to {0}", m_useDeferredRendering ? "true" : "false");
        }

        void OnUseClientSizeChanged(object sender, ValueChangedEventArgs args)
        {
            m_useClientSize = StateManager.Instance.XmlStateManager.GetValueAs<bool>(DDx._DDx_USECLIENTSIZE);
            Logger.InfoFormat("UseClientSize set to {0}", m_useClientSize ? "true" : "false");
            AdvanceRendering();
        }

        void OnShowMousePosChanged(object sender, ValueChangedEventArgs args)
        {
            m_showMousePos = StateManager.Instance.XmlStateManager.GetValueAs<bool>(DDx._DDx_SHOWMOUSEPOS);
            Logger.InfoFormat("ShowMousePos set to {0}", m_showMousePos ? "true" : "false");
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
            if (m_useClientSize)
            {
                return m_clientSize; 
            }
            else
            {
                return DefaultSize;
            }
        }

        public bool RequiresRender()
        {
            return false;
        }

        bool GenerateImages(Size imageSize, out Image image)
        {
            image = null;
            Image currentImage = null;
            bool needGenerateImages = false;

            switch (m_imgGenerationState)
            {
                case ImageGenerateState.Uninitialized:
                    // We haven't started generating any images yet.
                    needGenerateImages = true;
                    break;

                case ImageGenerateState.Generating:
                    // The images that we're generating are the wrong size
                    if (m_imgGenerationSize != imageSize)
                        needGenerateImages = true;
                    break;

                case ImageGenerateState.Complete:
                    // The completed images that we have generated are the wrong size
                    if (m_imageList.Any())
                    {
                        currentImage = m_imageList[m_currentImage % m_imageList.Count()];
                        if (currentImage != null)
                        {
                            if (imageSize != currentImage.Size)
                                needGenerateImages = true;
                        }
                    }
                    break;
            }

            if (needGenerateImages)
            {
                m_imgGenerationState = ImageGenerateState.Generating;
                m_imgGenerationSize = imageSize;

                if (cts != null)
                    cts.Cancel();

                cts = new CancellationTokenSource();
                Task.Factory.StartNew(() => InitializeImages(cts.Token));
            }
            else
            {
                if (currentImage != null)
                {
                    image = currentImage;
                }
            }
            return needGenerateImages;
        }

        public virtual void RenderView(RenderTarget target)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var image = target.Image;
            Image current = null;

            if (!GenerateImages(image.Size, out current))
            {
                if (current != null)
                {
                    current.CopyTo(ref image);
                }
            }

            if (m_showMousePos)
            {
                Canvas canvas = new Canvas(ref image);
                PureWebColor red = PureWebColor.FromKnownColor(PureWebKnownColor.Red);
                canvas.FillCircle(red, m_mouseX, m_mouseY, 22);
            }
            Trace.WriteLine("RenderView " + sw.Elapsed.TotalMilliseconds.ToString() + " ms");
        }

        public void PostKeyEvent(PureWebKeyboardEventArgs keyEvent)
        {
            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                String path = String.Format("/DDx/{0}/KeyEvent", m_viewName);
                state.SetValue(path + "/Type", keyEvent.EventType);
                state.SetValue(path + "/KeyCode", keyEvent.KeyCode);
                state.SetValue(path + "/CharacterCode", keyEvent.CharacterCode);
                state.SetValue(path + "/Modifiers", keyEvent.Modifiers);
            }
        }

        public void PostMouseEvent(PureWebMouseEventArgs mouseEvent)
        {
            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                String path = String.Format("/DDx/{0}/MouseEvent", m_viewName);
                state.SetValue(path + "/Type", mouseEvent.EventType);
                state.SetValue(path + "/X", mouseEvent.X);
                state.SetValue(path + "/Y", mouseEvent.Y);
                state.SetValue(path + "/Buttons", mouseEvent.Buttons);
                state.SetValue(path + "/ChangedButton", mouseEvent.ChangedButton);
                state.SetValue(path + "/Modifiers", mouseEvent.Modifiers);
            }

            if (mouseEvent.EventType == MouseEventType.MouseMove)
            {
                m_mouseX = (int)mouseEvent.X;
                m_mouseY = (int)mouseEvent.Y;

                if (!m_asyncImageGeneration)
                {
                    AdvanceRendering();
                }
            }

            if (mouseEvent.EventType == MouseEventType.MouseDown)
                StateManager.Instance.ViewManager.SetViewInteracting(m_viewName, true);

            if (mouseEvent.EventType == MouseEventType.MouseUp)
                StateManager.Instance.ViewManager.SetViewInteracting(m_viewName, false);
        }

        void Render()
        {
            if (m_useDeferredRendering)
            {
                m_remoteRenderer.RenderViewDeferred(m_viewName);
            }
            else
            {
                m_remoteRenderer.RenderViewImmediate(m_viewName);
            }
        }

        void OnViewRendered(object sender, ViewRenderedEventArgs args)
        {
            if(m_rapidImageGeneration)
            {
                UiDispatcher.BeginInvoke(AdvanceRendering);
            }
        }

    }
}
