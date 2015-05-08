using Aardvark.Base;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aardvark.Application;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Windows.Controls;
using System.Windows.Input;

namespace CSharpStuff
{
    public interface IHciMouseAsync
    {
        IEvent Enter { get; }
        IEvent Leave { get; }
        IEvent<bool> PressedLeft { get; }
        IEvent<bool> PressedMiddle { get; }
        IEvent<bool> PressedRight { get; }
        IEvent<PixelPosition> DownLeft { get; }
        IEvent<PixelPosition> DownMiddle { get; }
        IEvent<PixelPosition> DownRight { get; }
        IEvent<PixelPosition> UpLeft { get; }
        IEvent<PixelPosition> UpMiddle { get; }
        IEvent<PixelPosition> UpRight { get; }
        IEvent<PixelPosition> Move { get; }
        IEvent<PixelPosition> DoubleClickLeft { get; }
        IEvent<int> Wheel { get; }
    }
    public interface IHciKeyboardAsync
    {
        IEvent<Keys> KeyDown { get; }
        IEvent<Keys> KeyUp { get; }
        IEvent<Keys> KeyPressed { get; }

        IEvent<Keys> GetKeyDown(Keys k);
        IEvent<Keys> GetKeyUp(Keys k);
        IEvent<bool> GetKeyPressed(Keys k);

        /// <summary>
        /// Signals when all specified keys are down.
        /// </summary>
        IEvent<Keys[]> GetKeysDown(params Keys[] ks);

        /// <summary>
        /// True when all specified keys are down.
        /// </summary>
        IEvent<bool> GetKeysPressed(params Keys[] ks);
    }
    public abstract class HciKeyboard : IHciKeyboardAsync
    {
        protected EventSource<Keys> m_keyDown = new EventSource<Keys>();
        protected EventSource<Keys> m_keyUp = new EventSource<Keys>();
        protected EventSource<Keys> m_keyPressed = new EventSource<Keys>();
        private Dictionary<Keys, EventSource<Keys>> m_specificKeyDown = new Dictionary<Keys, EventSource<Keys>>();
        private Dictionary<Keys, EventSource<Keys>> m_specificKeyUp = new Dictionary<Keys, EventSource<Keys>>();
        private Dictionary<Keys, EventSource<bool>> m_specificKeyPressed = new Dictionary<Keys, EventSource<bool>>();

        /// <summary>
        /// Occurs when a key is physically pressed down (but not for repeat key events).
        /// </summary>
        public IEvent<Keys> KeyDown { get { return m_keyDown; } }
        /// <summary>
        /// Occurs when a key is released.
        /// </summary>
        public IEvent<Keys> KeyUp { get { return m_keyUp; } }
        /// <summary>
        /// Occurs when a key is logically pressed (also for repeat key events).
        /// </summary>
        public IEvent<Keys> KeyPressed { get { return m_keyPressed; } }

        public IEvent<Keys> GetKeyDown(Keys k)
        {
            lock (m_specificKeyDown)
            {
                if (!m_specificKeyDown.ContainsKey(k))
                {
                    var result = new EventSource<Keys>();
                    m_keyDown.Values.Where(x => x == k).Subscribe(_ => result.Emit(k));
                    m_specificKeyDown[k] = result;
                }

                return m_specificKeyDown[k];
            }
        }
        public IEvent<Keys> GetKeyUp(Keys k)
        {
            lock (m_specificKeyUp)
            {
                if (!m_specificKeyUp.ContainsKey(k))
                {
                    var result = new EventSource<Keys>();
                    m_keyUp.Values.Where(x => x == k).Subscribe(_ => result.Emit(k));
                    m_specificKeyUp[k] = result;
                }

                return m_specificKeyUp[k];
            }
        }
        public IEvent<bool> GetKeyPressed(Keys k)
        {
            lock (m_specificKeyPressed)
            {
                if (!m_specificKeyPressed.ContainsKey(k))
                {
                    var result = new EventSource<bool>();
                    m_keyPressed.Values.Where(x => x == k).Subscribe(_ => result.Emit(true));
                    m_keyUp.Values.Where(x => x == k).Subscribe(_ => result.Emit(false));
                    m_specificKeyPressed[k] = result;
                }

                return m_specificKeyPressed[k];
            }
        }

        /// <summary>
        /// Signals when all specified keys are down.
        /// </summary>
        public IEvent<Keys[]> GetKeysDown(params Keys[] ks)
        {
            if (ks == null) throw new ArgumentNullException();
            if (ks.Length < 2) throw new ArgumentException("Use GetKeyDown for single key.");

            var result = new EventSource<Keys[]>();
            var state = new Dictionary<Keys, bool>();
            foreach (var k in ks) state[k] = false;

            m_keyPressed.Values
                .Where(x => state.ContainsKey(x))
                .Subscribe(x =>
                {
                    state[x] = true;
                    if (state.Values.All(y => y == true)) result.Emit(ks);
                });
            m_keyUp.Values
                .Where(x => state.ContainsKey(x))
                .Subscribe(x =>
                {
                    state[x] = false;
                });

            return result;
        }

        /// <summary>
        /// True when all specified keys are down.
        /// </summary>
        public IEvent<bool> GetKeysPressed(params Keys[] ks)
        {
            if (ks == null) throw new ArgumentNullException();
            if (ks.Length < 2) throw new ArgumentException("Use GetKeyPressed for single key.");

            var result = new EventSource<bool>();
            var state = new Dictionary<Keys, bool>();
            foreach (var k in ks) state[k] = false;

            m_keyPressed.Values
                .Where(x => state.ContainsKey(x))
                .Subscribe(x =>
                {
                    state[x] = true;
                    if (state.Values.All(y => y == true)) result.Emit(true);
                });
            m_keyUp.Values
                .Where(x => state.ContainsKey(x))
                .Subscribe(x =>
                {
                    state[x] = false;
                    result.Emit(false);
                });

            return result;
        }
    }

    public abstract class HciMouse : IHciMouseAsync
    {
        protected EventSource<Unit> m_enter = new EventSource<Unit>();
        protected EventSource<Unit> m_leave = new EventSource<Unit>();
        protected EventSource<bool> m_pressedLeft = new EventSource<bool>();
        protected EventSource<bool> m_pressedMiddle = new EventSource<bool>();
        protected EventSource<bool> m_pressedRight = new EventSource<bool>();
        protected EventSource<PixelPosition> m_downLeft = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_downMiddle = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_downRight = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_upLeft = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_upMiddle = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_upRight = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_move = new EventSource<PixelPosition>();
        protected EventSource<PixelPosition> m_doubleClickLeft = new EventSource<PixelPosition>();
        protected EventSource<int> m_wheel = new EventSource<int>();

        public IEvent Enter { get { return m_enter; } }
        public IEvent Leave { get { return m_enter; } }
        public IEvent<PixelPosition> DownLeft { get { return m_downLeft; } }
        public IEvent<PixelPosition> DownMiddle { get { return m_downMiddle; } }
        public IEvent<PixelPosition> DownRight { get { return m_downRight; } }
        public IEvent<PixelPosition> UpLeft { get { return m_upLeft; } }
        public IEvent<PixelPosition> UpMiddle { get { return m_upMiddle; } }
        public IEvent<PixelPosition> UpRight { get { return m_upRight; } }
        public IEvent<PixelPosition> Move { get { return m_move; } }
        public IEvent<PixelPosition> DoubleClickLeft { get { return m_doubleClickLeft; } }
        public IEvent<int> Wheel { get { return m_wheel; } }
        public IEvent<bool> PressedLeft { get { return m_pressedLeft; } }
        public IEvent<bool> PressedMiddle { get { return m_pressedMiddle; } }
        public IEvent<bool> PressedRight { get { return m_pressedRight; } }
    }

    public class HciKeyboardWinFormsAsync : HciKeyboard
    {
        private System.Windows.Forms.Control m_control;
        private Dictionary<Keys, bool> m_keys = new Dictionary<Keys, bool>();

        public HciKeyboardWinFormsAsync(System.Windows.Forms.Control control)
        {
            Requires.NotNull(control);
            m_control = control;

            m_keyDown.Values.Subscribe(k => m_keys[k] = true);
            m_keyUp.Values.Subscribe(k => m_keys[k] = false);

            m_control.PreviewKeyDown += (s, e) =>
            {
                var k = Aardvark.Application.KeyConverter.keyFromVirtualKey((int)e.KeyCode);
                //Report.Line("HciKeyboardWinFormsAsync.KeyDown: KeyCode {0}, KeyValue {1}", e.KeyCode, e.KeyValue);

                if (k == Keys.LeftAlt)
                {
                    //m_keyDown.Emit(Keys.Alt);
                    if (!IsPressed(k)) m_keyDown.Emit(Keys.LeftAlt);
                    m_keyPressed.Emit(Keys.LeftAlt);
                }

                //m_keyDown.Emit(k);
                if (!IsPressed(k)) m_keyDown.Emit(k);
                m_keyPressed.Emit(k);
            };
            m_control.KeyUp += (s, e) =>
            {
                var k = Aardvark.Application.KeyConverter.keyFromVirtualKey((int)e.KeyCode);
                //Report.Line("HciKeyboardWinFormsAsync.KeyUp: KeyCode {0}, KeyValue {1}", e.KeyCode, e.KeyValue);

                if (k == Keys.LeftAlt)
                {
                    m_keyUp.Emit(Keys.LeftAlt);
                }
                m_keyUp.Emit(k);
            };
        }

        private bool IsPressed(Keys k)
        {
            bool isPressed = false;
            m_keys.TryGetValue(k, out isPressed);
            return isPressed;
        }

        public System.Windows.Forms.Control Control { get { return m_control; } }
    }

    public class HciMouseWinFormsAsync : HciMouse
    {
        private System.Windows.Forms.Control m_control;

        public HciMouseWinFormsAsync(System.Windows.Forms.Control control)
        {
            Requires.NotNull(control);
            m_control = control;

            m_control.MouseEnter += (s, e) => m_enter.Emit(Unit.Default);

            m_control.MouseLeave += (s, e) => m_leave.Emit(Unit.Default);

            m_control.MouseDown += (s, e) =>
            {
                switch (e.Button)
                {
                    case System.Windows.Forms.MouseButtons.Left:
                        m_downLeft.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));
                        m_pressedLeft.Emit(true);
                        break;
                    case System.Windows.Forms.MouseButtons.Middle:
                        m_downMiddle.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));
                        m_pressedMiddle.Emit(true);
                        break;
                    case System.Windows.Forms.MouseButtons.Right:
                        m_downRight.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));
                        m_pressedRight.Emit(true);
                        break;
                }
            };

            m_control.MouseUp += (s, e) =>
            {
                switch (e.Button)
                {
                    case System.Windows.Forms.MouseButtons.Left:
                        m_upLeft.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));
                        m_pressedLeft.Emit(false);
                        break;
                    case System.Windows.Forms.MouseButtons.Middle:
                        m_upMiddle.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));
                        m_pressedMiddle.Emit(false);
                        break;
                    case System.Windows.Forms.MouseButtons.Right:
                        m_upRight.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));
                        m_pressedRight.Emit(false);
                        break;
                }
            };

            m_control.MouseDoubleClick += (s, e) => m_doubleClickLeft.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));

            m_control.MouseMove += (s, e) => m_move.Emit(new PixelPosition(e.X, e.Y, m_control.Width, m_control.Height));

            m_control.MouseWheel += (s, e) => m_wheel.Emit(e.Delta / 120);
        }

        public System.Windows.Forms.Control Control { get { return m_control; } }
    }

    public class HciKeyboardWpfAsync : HciKeyboard
    {
        private Control m_control;

        public HciKeyboardWpfAsync(Control control)
        {
            Requires.NotNull(control);
            m_control = control;

            m_control.KeyDown += (s, e) =>
            {
                var k = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
                if (!e.IsRepeat) m_keyDown.Emit(k);
                m_keyPressed.Emit(k);
            };

            m_control.KeyUp += (s, e) =>
            {
                m_keyUp.Emit((Keys)KeyInterop.VirtualKeyFromKey(e.Key));
            };
        }

        public Control Control { get { return m_control; } }
    }

    public class DefaultCameraControllers
    {
        private EventSource<bool> m_isEnabled = new EventSource<bool>(false);

        private double m_mouseFactor = 0.01;
        private double m_moveSpeed = 10.0;
        private double m_mouseWheelSpeed = 20.0;
        private CancellationToken m_ct;
        private IHciMouseAsync m_mouse;
        private IHciKeyboardAsync m_keyboard;
        private ICameraView m_camera;
        private Clock m_clock;

        public DefaultCameraControllers(
            IHciMouseAsync mouse, IHciKeyboardAsync keyboard, ICameraView camera,
            double mouseSpeed = 0.01, double moveSpeed = 0.01, double wheelSpeed = 20.0,
            CancellationToken ct = default(CancellationToken),
            IEvent<bool> isEnabled = null
            )
        {
            m_clock = new Clock(120);
            m_mouse = mouse;
            m_keyboard = keyboard;
            m_camera = camera;
            m_ct = ct;
            m_mouseFactor = mouseSpeed;
            m_moveSpeed = moveSpeed;
            m_mouseWheelSpeed = wheelSpeed;

            if (isEnabled != null)
            {
                m_isEnabled.Emit(isEnabled.Latest);
                isEnabled.Values.Subscribe(v => m_isEnabled.Emit(v));
            }

            if (m_mouse != null)
            {
                CreateMouseLookAroundController()();
                CreateMousePanController()();
                CreateMouseBackAndForthController()();
                CreateMouseWheelController()();
            }

            if (m_keyboard != null)
            {
                CreateKeyboardASDWController()();
            }
        }

        public double MouseFactor
        {
            get { return m_mouseFactor; }
            set { m_mouseFactor = value; }
        }

        public double MoveSpeed
        {
            get { return m_moveSpeed; }
            set { m_moveSpeed = value; }
        }

        public double MouseWheelSpeed
        {
            get { return m_mouseWheelSpeed; }
            set { m_mouseWheelSpeed = value; }
        }

        private Action CreateKeyboardASDWController()
        {
            return async delegate
            {
                try
                {
                    var forward = m_keyboard.GetKeyPressed(Keys.W);
                    var backward = m_keyboard.GetKeyPressed(Keys.S);
                    var left = m_keyboard.GetKeyPressed(Keys.A);
                    var right = m_keyboard.GetKeyPressed(Keys.D);

                    while (true)
                    {
                        await Await.WhenAny<bool>(forward.Next, backward.Next, left.Next, right.Next).WithCancellation(m_ct);

                        while (forward.Latest || backward.Latest || left.Latest || right.Latest)
                        {

                            var time = await m_clock.Tick();
                            var distande = time.Delta * m_moveSpeed;
                            if (m_isEnabled.Latest)
                            {
                                if (forward.Latest) m_camera.Location += m_moveSpeed * m_camera.Forward;
                                if (backward.Latest) m_camera.Location -= m_moveSpeed * m_camera.Forward;
                                if (right.Latest) m_camera.Location += m_moveSpeed * m_camera.Right;
                                if (left.Latest) m_camera.Location -= m_moveSpeed * m_camera.Right;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Report.Line("KeyboardASDWController cancelled");
                }
                catch (Exception e)
                {
                    Report.Warn("KeyboardASDWController faulted: {0}", e);
                }
            };
        }

        private Action CreateMousePanController()
        {
            return async delegate
            {
                try
                {
                    while (true)
                    {
                        m_ct.ThrowIfCancellationRequested();

                        var start = await m_mouse.DownMiddle.Next.WithCancellation(m_ct);
                        var finish = m_mouse.UpMiddle.Next;

                        var lastMousePosition = start;

                        while (true)
                        {
                            var nextMousePosition = m_mouse.Move.Next;

                            await Await.WhenAny(nextMousePosition, finish).WithCancellation(m_ct);
                            if (finish.IsCompleted) break;

                            var currentMousePosition = nextMousePosition.Result;
                            var d = m_mouseFactor * (V2d)(currentMousePosition.Position - lastMousePosition.Position);
                            if (m_isEnabled.Latest) m_camera.Location += d.X * m_camera.Right - d.Y * m_camera.Up;
                            lastMousePosition = currentMousePosition;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Report.Line("MousePanController cancelled");
                }
                catch (Exception e)
                {
                    Report.Warn("MousePanController faulted: {0}", e);
                }
            };
        }

        private Action CreateMouseLookAroundController()
        {
            return async delegate
            {
                try
                {
                    while (true)
                    {
                        m_ct.ThrowIfCancellationRequested();

                        var start = await m_mouse.DownLeft.Next.WithCancellation(m_ct);
                        var finish = m_mouse.UpLeft.Next;

                        var lastMousePosition = start;

                        while (true)
                        {
                            var nextMousePosition = m_mouse.Move.Next;

                            await Await.WhenAny(nextMousePosition, finish).WithCancellation(m_ct);
                            if (finish.IsCompleted) break;

                            var currentMousePosition = nextMousePosition.Result;
                            if (m_isEnabled.Latest)
                            {
                                var d = 0.5 * m_mouseFactor * (V2d)(currentMousePosition.Position - lastMousePosition.Position);
                                Rot3d yaw = new Rot3d(m_camera.Up, -d.X);
                                Rot3d pitch = new Rot3d(m_camera.Right, -d.Y);
                                var rot = yaw * pitch;
                                m_camera.Forward = rot.TransformDir(m_camera.Forward);
                            }
                            lastMousePosition = currentMousePosition;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Report.Line("MouseLookAroundController cancelled");
                }
                catch (Exception e)
                {
                    Report.Warn("MouseLookAroundController faulted: {0}", e);
                }
            };
        }

        private Action CreateMouseBackAndForthController()
        {
            return async delegate
            {
                try
                {
                    while (true)
                    {
                        m_ct.ThrowIfCancellationRequested();

                        var start = await m_mouse.DownRight.Next.WithCancellation(m_ct);
                        var finish = m_mouse.UpRight.Next;

                        var lastMousePosition = start;

                        while (true)
                        {
                            var nextMousePosition = m_mouse.Move.Next;

                            await Await.WhenAny(nextMousePosition, finish).WithCancellation(m_ct);
                            if (finish.IsCompleted) break;
                            var currentMousePosition = nextMousePosition.Result;
                            if (m_isEnabled.Latest)
                            {
                                var d = -m_mouseFactor * (currentMousePosition.Position.Y - lastMousePosition.Position.Y);
                                m_camera.Location += d * m_camera.Forward;
                            }
                            lastMousePosition = currentMousePosition;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Report.Line("MouseLookAroundController cancelled");
                }
                catch (Exception e)
                {
                    Report.Warn("MouseBackAndForthController faulted: {0}", e);
                }
            };
        }

        private Action CreateMouseWheelController()
        {
            return async delegate
            {
                try
                {
                    double momentum = 0.0;
                    var nextWheel = m_mouse.Wheel.Next;
                    while (true)
                    {
                        m_ct.ThrowIfCancellationRequested();

                        var time = await m_clock.Tick();

                        if (momentum == 0.0)
                        {
                            var x = await nextWheel.WithCancellation(m_ct);
                            momentum += 10.0 * m_mouseFactor * x * m_mouseWheelSpeed;
                        }
                        else
                        {
                            if (nextWheel.IsCompleted)
                            {
                                momentum += 10.0 * m_mouseFactor * nextWheel.Result * m_mouseWheelSpeed;
                                nextWheel = m_mouse.Wheel.Next;
                            }

                            momentum *= 0.95;
                            if (momentum.Abs() < 0.002) momentum = 0.0;
                        }

                        if (m_isEnabled.Latest) m_camera.Location += m_camera.Forward * momentum * time.Delta;
                    }
                }
                catch (TaskCanceledException)
                {
                    Report.Line("MouseWheelController cancelled");
                }
                catch (Exception e)
                {
                    Report.Warn("MouseWheelController faulted: {0}", e);
                }
            };
        }

        public ICameraView ControlledCameraView
        {
            get { return m_camera; }
        }

        public ICameraProjection ControlledCameraProjection
        {
            get { return null; }
        }

        public EventSource<bool> IsCameraControllerEnabled
        {
            get { return m_isEnabled; }
        }

        public async Task EnableCameraControllerWithTransition(double transitionTimeInSeconds, IEvent<double> time)
        {
            // no animation necessary, since this controller only applies deltas to current camera view
            IsCameraControllerEnabled.Emit(true);
            await Task.Yield();
        }
    }

    public class HciMouseWpfAsync : HciMouse
    {
        private Control m_control;

        public HciMouseWpfAsync(Control control)
        {
            Requires.NotNull(control);
            m_control = control;

            m_control.MouseEnter += (s, e) => m_enter.Emit(Unit.Default);

            m_control.MouseLeave += (s, e) => m_leave.Emit(Unit.Default);

            m_control.MouseDown += (s, e) =>
            {
                var pRaw = e.GetPosition(m_control);
                var p = new PixelPosition((int)pRaw.X, (int)pRaw.Y, (int)m_control.ActualWidth, (int)m_control.ActualHeight);
                switch (e.ChangedButton)
                {
                    case MouseButton.Left:
                        m_downLeft.Emit(p);
                        m_pressedLeft.Emit(true);
                        break;
                    case MouseButton.Middle:
                        m_downMiddle.Emit(p);
                        m_pressedMiddle.Emit(true);
                        break;
                    case MouseButton.Right:
                        m_downRight.Emit(p);
                        m_pressedRight.Emit(true);
                        break;
                }
            };

            m_control.MouseUp += (s, e) =>
            {
                var pRaw = e.GetPosition(m_control);
                var p = new PixelPosition((int)pRaw.X, (int)pRaw.Y, (int)m_control.ActualWidth, (int)m_control.ActualHeight);
                switch (e.ChangedButton)
                {
                    case MouseButton.Left:
                        m_upLeft.Emit(p);
                        m_pressedLeft.Emit(false);
                        break;
                    case MouseButton.Middle:
                        m_upMiddle.Emit(p);
                        m_pressedMiddle.Emit(false);
                        break;
                    case MouseButton.Right:
                        m_upRight.Emit(p);
                        m_pressedRight.Emit(false);
                        break;
                }
            };

            m_control.MouseMove += (s, e) =>
            {
                var pRaw = e.GetPosition(m_control);
                var p = new PixelPosition((int)pRaw.X, (int)pRaw.Y, (int)m_control.ActualWidth, (int)m_control.ActualHeight);
                m_move.Emit(p);
            };

            m_control.MouseDoubleClick += (s, e) =>
            {
                var pRaw = e.GetPosition(m_control);
                var p = new PixelPosition((int)pRaw.X, (int)pRaw.Y, (int)m_control.ActualWidth, (int)m_control.ActualHeight);
                m_doubleClickLeft.Emit(p);
            };

            m_control.MouseWheel += (s, e) => m_wheel.Emit(e.Delta / 120);
        }

        public Control Control { get { return m_control; } }
    }

}
