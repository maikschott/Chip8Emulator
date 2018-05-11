using System;

namespace Chip8Emulator
{
  public class GraphicsUnit
  {
    public const int ColumnCount = 64;
    public const int RowCount = 32;

    private readonly byte[] display = new byte[RowCount * ColumnCount];
    private bool needRedraw = true;

    public Action<byte[]> DoDraw { get; set; }

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
  }
}
