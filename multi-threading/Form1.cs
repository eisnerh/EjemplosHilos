using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace multi_threading
{
    public struct DrawnShape
    {
        public Rectangle Bounds { get; }
        public Color Color { get; }

        public DrawnShape(Rectangle bounds, Color color)
        {
            Bounds = bounds;
            Color = color;
        }
    }

    public class ShapeCanvasState
    {
        private readonly object _syncRoot = new object();
        private readonly List<DrawnShape> _shapes = new List<DrawnShape>();

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _shapes.Count;
                }
            }
        }

        public void Add(DrawnShape shape)
        {
            lock (_syncRoot)
            {
                _shapes.Add(shape);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _shapes.Clear();
            }
        }

        public DrawnShape[] Snapshot()
        {
            lock (_syncRoot)
            {
                return _shapes.ToArray();
            }
        }

        public int GenerateBatch(
            Color color,
            int count,
            int canvasWidth,
            int canvasHeight,
            int shapeSize,
            int delayMs,
            CancellationToken cancellationToken,
            Action onShapeAdded = null,
            int? seed = null)
        {
            int maxX = Math.Max(1, canvasWidth - shapeSize);
            int maxY = Math.Max(1, canvasHeight - shapeSize);
            Random localRandom = seed.HasValue
                ? new Random(seed.Value)
                : new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId));

            int added = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (delayMs > 0)
                {
                    if (cancellationToken.WaitHandle.WaitOne(delayMs))
                    {
                        break;
                    }
                }

                var rect = new Rectangle(
                    localRandom.Next(0, maxX),
                    localRandom.Next(0, maxY),
                    shapeSize,
                    shapeSize);

                Add(new DrawnShape(rect, color));
                added++;
                onShapeAdded?.Invoke();
            }

            return added;
        }
    }

    public partial class frmMultiThreading : Form
    {
        private readonly ShapeCanvasState _canvasState = new ShapeCanvasState();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private int _redRunning;
        private int _blueRunning;

        public frmMultiThreading()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        public ShapeCanvasState CanvasState => _canvasState;

        private void btnRed_Click(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref _redRunning, 1, 0) != 0)
            {
                return;
            }

            btnRed.Enabled = false;
            int width = Math.Max(40, this.ClientSize.Width);
            int height = Math.Max(40, this.ClientSize.Height);
            CancellationToken token = _cts.Token;

            Task.Run(() => RunWorker(Color.Red, width, height, token, () =>
            {
                Interlocked.Exchange(ref _redRunning, 0);
                SafeUi(() =>
                {
                    btnRed.Enabled = true;
                    MessageBox.Show(this, "Completed Red", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }));
        }

        public void threadRed()
        {
            int width = Math.Max(40, this.ClientSize.Width);
            int height = Math.Max(40, this.ClientSize.Height);
            _canvasState.GenerateBatch(Color.Red, 100, width, height, 20, 50, _cts.Token, RequestRedraw);
        }

        private void btnBlue_Click(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref _blueRunning, 1, 0) != 0)
            {
                return;
            }

            btnBlue.Enabled = false;
            int width = Math.Max(40, this.ClientSize.Width);
            int height = Math.Max(40, this.ClientSize.Height);
            CancellationToken token = _cts.Token;

            Task.Run(() => RunWorker(Color.Blue, width, height, token, () =>
            {
                Interlocked.Exchange(ref _blueRunning, 0);
                SafeUi(() =>
                {
                    btnBlue.Enabled = true;
                    MessageBox.Show(this, "Completed Blue", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }));
        }

        public void threadBlue()
        {
            int width = Math.Max(40, this.ClientSize.Width);
            int height = Math.Max(40, this.ClientSize.Height);
            _canvasState.GenerateBatch(Color.Blue, 100, width, height, 20, 50, _cts.Token, RequestRedraw);
        }

        private void RunWorker(Color color, int width, int height, CancellationToken token, Action onCompleted)
        {
            try
            {
                int generated = _canvasState.GenerateBatch(color, 100, width, height, 20, 50, token, RequestRedraw);
                if (!token.IsCancellationRequested && generated == 100)
                {
                    onCompleted();
                }
                else
                {
                    if (color == Color.Red) Interlocked.Exchange(ref _redRunning, 0);
                    if (color == Color.Blue) Interlocked.Exchange(ref _blueRunning, 0);
                    SafeUi(() =>
                    {
                        if (color == Color.Red) btnRed.Enabled = true;
                        if (color == Color.Blue) btnBlue.Enabled = true;
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                // Form closed while worker was finishing.
            }
        }

        private void RequestRedraw()
        {
            SafeUi(this.Invalidate);
        }

        private void SafeUi(Action action)
        {
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                return;
            }

            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawnShape[] snapshot = _canvasState.Snapshot();
            if (snapshot.Length == 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen redPen = new Pen(Color.Red, 4f))
            using (Pen bluePen = new Pen(Color.Blue, 4f))
            {
                foreach (DrawnShape shape in snapshot)
                {
                    Pen pen = shape.Color == Color.Red ? redPen : bluePen;
                    e.Graphics.DrawRectangle(pen, shape.Bounds);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts.Cancel();
            base.OnFormClosing(e);
        }

        private void frmMultiThreading_Load(object sender, EventArgs e)
        {
            this.Text = "MultiThreading (Thread-Safe DoubleBuffered)";
        }
    }
}
