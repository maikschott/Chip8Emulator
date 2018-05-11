using System;

namespace Chip8Emulator
{
  public class GraphicsUnit
  {
    public readonly byte[] FontSet =
    {
      0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
      0x20, 0x60, 0x20, 0x20, 0x70, // 1
      0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
      0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
      0x90, 0x90, 0xF0, 0x10, 0x10, // 4
      0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
      0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
      0xF0, 0x10, 0x20, 0x40, 0x40, // 7
      0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
      0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
      0xF0, 0x90, 0xF0, 0x90, 0x90, // A
      0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
      0xF0, 0x80, 0x80, 0x80, 0xF0, // C
      0xE0, 0x90, 0x90, 0x90, 0xE0, // D
      0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
      0xF0, 0x80, 0xF0, 0x80, 0x80 // F
    };

    private byte[] display;
    private bool highRes;
    private bool needRedraw = true;

    public GraphicsUnit()
    {
      display = new byte[RowCount * ColumnCount];
    }

    public int ColumnCount { get; private set; } = 64;
    public int RowCount { get; private set; } = 32;

    public Action<byte[]> DoDraw { private get; set; }
    public Action<bool> ChangeResolution { private get; set; }

    public bool HighRes
    {
      get => highRes;
      set
      {
        if (highRes == value) { return; }

        highRes = value;
        if (value)
        {
          ColumnCount = 128;
          RowCount = 64;
        }
        else
        {
          ColumnCount = 64;
          RowCount = 32;
        }
        display = new byte[RowCount * ColumnCount];
        ChangeResolution?.Invoke(value);
      }
    }

    public void ClearScreen()
    {
      Array.Clear(display, 0, display.Length);
      needRedraw = true;
    }

    public bool GetPixel(int x, int y)
    {
      return display[(y * ColumnCount + x) % display.Length] != 0;
    }

    public bool TogglePixel(int x, int y)
    {
      var i = (y * ColumnCount + x) % display.Length;
      display[i] ^= 255;
      needRedraw = true;
      return display[i] != 0;
    }

    public void Draw()
    {
      if (!needRedraw) { return; }
      needRedraw = false;

      DoDraw?.Invoke(display);
    }

    public void ScrollUp(int pixels)
    {
      if (pixels == 0) { return; }

      Array.Copy(display, ColumnCount, display, 0, (RowCount - 1) * ColumnCount);
      Array.Clear(display, (RowCount - 1) * ColumnCount, ColumnCount);

      needRedraw = true;
    }

    public void ScrollDown(int pixels)
    {
      if (pixels == 0) { return; }

      Array.Copy(display, 0, display, ColumnCount, (RowCount - 1) * ColumnCount);
      Array.Clear(display, 0, ColumnCount);

      needRedraw = true;
    }

    public void ScrollLeft(int pixels)
    {
      if (pixels == 0) { return; }

      int index = 0;
      for (var y = 0; y < RowCount; y++)
      {
        for (var x = 0; x < ColumnCount - pixels; x++)
        {
          display[index] = display[index + pixels];
          index++;
        }
        for (var x = 0; x < pixels; x++)
        {
          display[index++] = 0;
        }
      }

      needRedraw = true;
    }

    public void ScrollRight(int pixels)
    {
      if (pixels == 0) { return; }

      int index = display.Length - 1;
      for (var y = 0; y < RowCount; y++)
      {
        for (var x = 0; x < ColumnCount - pixels; x++)
        {
          display[index] = display[index - pixels];
          index--;
        }
        for (var x = 0; x < pixels; x++)
        {
          display[index--] = 0;
        }
      }

      needRedraw = true;
    }
  }
}