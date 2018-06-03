using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chip8Emulator
{
  public class Screen : Form
  {
    private readonly Dictionary<Keys, byte> keyMappings = new Dictionary<Keys, byte>
    {
      [Keys.D1] = 0x1,
      [Keys.D2] = 0x2,
      [Keys.D3] = 0x3,
      [Keys.D4] = 0xC,
      [Keys.Q] = 0x4,
      [Keys.W] = 0x5,
      [Keys.E] = 0x6,
      [Keys.R] = 0xD,
      [Keys.A] = 0x7,
      [Keys.S] = 0x8,
      [Keys.D] = 0x9,
      [Keys.F] = 0xE,
      [Keys.Y] = 0xA,
      [Keys.X] = 0x0,
      [Keys.C] = 0xB,
      [Keys.V] = 0xF
    };

    private readonly Machine machine;
    private readonly Task programTask;
    private readonly CancellationTokenSource cts;
    private Bitmap bitmap;
    private PictureBox image;

    public Screen(Machine machine, Task programTask, CancellationTokenSource cts)
    {
      this.machine = machine;
      this.programTask = programTask;
      this.cts = cts;
      InitializeComponent();
    }

    private void InitializeComponent()
    {
      Text = "Keypad: 1-4, Q-R, A-F, Y-V, Restart: F5, Emulation speed: +/-";

      image = new PictureBoxWithInterpolationMode
      {
        SizeMode = PictureBoxSizeMode.StretchImage,
        InterpolationMode = InterpolationMode.NearestNeighbor,
        Dock = DockStyle.Fill
      };
      CreateBitmap();
      BackColor = Color.Black;
      Controls.Add(image);

      bool isDisplayUpdating = false;

      machine.GraphicsUnit.DoDraw = displayData =>
      {
        if (isDisplayUpdating) { return; }
        isDisplayUpdating = true;

        var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
        try
        {
          Marshal.Copy(displayData, 0, bmpData.Scan0, displayData.Length);
        }
        finally
        {
          bitmap.UnlockBits(bmpData);
        }

        if (image.IsDisposed) { return; }
        image.Invoke((Action)delegate
        {
          image.Invalidate();
          image.Update();
        });

        isDisplayUpdating = false;
      };
      machine.GraphicsUnit.ChangeResolution = _ =>
      {
        image.Invoke((Action)CreateBitmap);
      };
    }

    private void CreateBitmap()
    {
      bitmap = new Bitmap(machine.GraphicsUnit.ColumnCount, machine.GraphicsUnit.RowCount, PixelFormat.Format8bppIndexed);
      image.Image = bitmap;
      ClientSize = new Size(bitmap.Width * 10, bitmap.Height * 10);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
      base.OnKeyDown(e);

      switch (e.KeyCode)
      {
        case Keys.Escape:
          Application.Exit();
          break;
        case Keys.F5:
          machine.EmulationSpeed = Machine.DefaultEmulationSpeed;
          machine.Reset();
          break;
        case Keys.Add:
          machine.EmulationSpeed = (int)(machine.EmulationSpeed * 1.1);
          break;
        case Keys.Subtract:
          machine.EmulationSpeed = (int)(machine.EmulationSpeed / 1.1);
          break;
      }

      if (keyMappings.TryGetValue(e.KeyCode, out byte key))
      {
        machine.Keys[key] = true;
      }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
      base.OnKeyUp(e);

      if (keyMappings.TryGetValue(e.KeyCode, out byte key))
      {
        machine.Keys[key] = false;
      }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
      cts.Cancel();
      await programTask;
      base.OnFormClosing(e);
    }
  }

  public class PictureBoxWithInterpolationMode : PictureBox
  {
    public InterpolationMode InterpolationMode { get; set; }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
      paintEventArgs.Graphics.InterpolationMode = InterpolationMode;
      paintEventArgs.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
      base.OnPaint(paintEventArgs);
    }
  }
}