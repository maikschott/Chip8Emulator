using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
    private Bitmap bitmap;
    private PictureBox image;

    public Screen(Machine machine)
    {
      this.machine = machine;
      InitializeComponent();
    }

    private void InitializeComponent()
    {
      bitmap = new Bitmap(Display.ColumnCount, Display.RowCount, PixelFormat.Format8bppIndexed);
      image = new PictureBoxWithInterpolationMode
      {
        SizeMode = PictureBoxSizeMode.StretchImage,
        InterpolationMode = InterpolationMode.NearestNeighbor,
        Dock = DockStyle.Fill,
        Image = bitmap
      };
      ClientSize = new Size(bitmap.Width * 10, bitmap.Height * 10);
      BackColor = Color.Black;
      Controls.Add(image);
      KeyDown += (sender, eventArgs) =>
      {
        if (eventArgs.KeyCode == Keys.Escape)
        {
          Application.Exit();
        }

        if (eventArgs.KeyCode == Keys.F5)
        {
          machine.Reset();
        }

        if (keyMappings.TryGetValue(eventArgs.KeyCode, out byte key))
        {
          machine.Keys[key] = true;
        }
      };
      KeyUp += (sender, eventArgs) =>
      {
        if (keyMappings.TryGetValue(eventArgs.KeyCode, out byte key))
        {
          machine.Keys[key] = false;
        }
      };

      machine.Display.DoDraw = bitarray =>
      {
        var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
        try
        {
          Marshal.Copy(bitarray, 0, bmpData.Scan0, bitarray.Length);
        }
        finally
        {
          bitmap.UnlockBits(bmpData);
        }

        image.Invoke((Action)delegate
        {
          image.Invalidate();
          image.Update();
        });
      };
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