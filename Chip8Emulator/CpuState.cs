using System.Collections.Generic;

namespace Chip8Emulator
{
  public class CpuState
  {
    public const int DefaultAddress = 0x200;

    public byte[] Registers { get; } = new byte[16]; // V0 to VF

    public ushort AddressRegister { get; set; } // I

    public ushort Pc { get; private set; } = DefaultAddress;

    public Stack<ushort> Callstack { get; } = new Stack<ushort>(16);

    public byte UserFlags { get; set; }

    public void Next()
    {
      Pc += 2;
    }

    public void Retry()
    {
      Pc -= 2;
    }

    public void Jump(ushort location)
    {
      Pc = location;
    }
  }
}
