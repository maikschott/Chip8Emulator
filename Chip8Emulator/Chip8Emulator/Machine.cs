using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Chip8Emulator
{
  public class Machine
  {
    public const int CpuFrequency = 60; // Hertz
    public const int MemorySize = 4096;
    private const byte False = 0;
    private const byte True = 1;

    private readonly byte[] fontSet =
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

    private readonly Random rnd;

    private readonly Stopwatch timer = new Stopwatch();

    public Machine()
    {
      Memory = new byte[MemorySize];
      Array.Copy(fontSet, Memory, fontSet.Length);
      Display = new Display();
      Keys = new bool[16];
      rnd = new Random();

      Reset();
    }

    public byte[] Memory { get; }

    public CpuState Cpu { get; private set; }

    public Display Display { get; }

    public byte DelayTimer { get; set; }

    public byte SoundTimer { get; set; }

    public bool[] Keys { get; set; }

    public void Reset()
    {
      Cpu = new CpuState();
      Display.ClearScreen();
      Array.Clear(Keys, 0, Keys.Length);
    }

    public void LoadProgram(string path)
    {
      var program = File.ReadAllBytes(path);
      LoadProgram(program);
    }

    public void LoadProgram(byte[] program)
    {
      Array.Copy(program, 0, Memory, Cpu.Pc, program.Length);
    }

    public void ExecuteProgram(CancellationToken cancellationToken)
    {
      timer.Restart();

      while (!cancellationToken.IsCancellationRequested)
      {
        ProcessLoop();
        Thread.Sleep(0);
      }
    }

    public void ProcessLoop()
    {
      PrintState();
      EmulateCycle();

      if (timer.ElapsedMilliseconds >= 1000 / CpuFrequency)
      {
        timer.Restart();
        if (DelayTimer > 0) { DelayTimer--; }
        if (SoundTimer > 0)
        {
          if (--SoundTimer == 0)
          {
            Console.Beep(800, 50);
          }
        }
      }

      Display.Draw();
    }

    [DllImport("user32.dll")]
    public static extern ushort GetAsyncKeyState(short nVirtKey);

    public static bool IsKeyPressed(ConsoleKey key)
    {
      const ushort KeyDown = 0x8000;
      return (GetAsyncKeyState((short)key) & KeyDown) == KeyDown;
    }

    private void PrintState()
    {
      Console.SetCursorPosition(0, 0);
      Console.WriteLine($"PC={Cpu.Pc:X4}, I={Cpu.AddressRegister:X4}");
      Console.WriteLine(string.Join(", ", Cpu.Registers.Select((x, i) => $"V{i:X}={x:X2}")));
    }

    private void EmulateCycle()
    {
      var opcode = (ushort)((Memory[Cpu.Pc] << 8) | Memory[Cpu.Pc + 1]);
      Cpu.Next();


      var x = (opcode >> 8) & 0xF;
      var y = (opcode >> 4) & 0xF;
      var nn = (byte)opcode;
      var nnn = (ushort)(opcode & 0x0FFF);
      var v = Cpu.Registers;

      switch (opcode >> 12)
      {
        case 0x0:
          if (opcode == 0x00E0)
          {
            Display.ClearScreen();
          }
          else if (opcode == 0x00EE)
          {
            Cpu.Jump(Cpu.Callstack.Pop());
          }
          break;
        case 0x1:
          Cpu.Jump(nnn);
          break;
        case 0x2:
          Cpu.Callstack.Push(Cpu.Pc);
          Cpu.Jump(nnn);
          break;
        case 0x3:
          if (v[x] == nn) { Cpu.Next(); }
          break;
        case 0x4:
          if (v[x] != nn) { Cpu.Next(); }
          break;
        case 0x5:
          if (v[x] == v[y]) { Cpu.Next(); }
          break;
        case 0x6:
          v[x] = (byte)opcode;
          break;
        case 0x7:
          v[x] += (byte)opcode;
          break;
        case 0x8:
          switch (opcode & 0xF)
          {
            case 0x0:
              v[x] = v[y];
              break;
            case 0x1:
              v[x] |= v[y];
              break;
            case 0x2:
              v[x] &= v[y];
              break;
            case 0x3:
              v[x] ^= v[y];
              break;
            case 0x4:
            {
              var result = v[x] + v[y];
              v[x] = (byte)result;
              v[0xf] = result > 255 ? True : False;
              break;
            }
            case 0x5:
            {
              var result = v[x] - v[y];
              v[x] = (byte)result;
              v[0xf] = result >= 0 ? True : False;
              break;
            }
            case 0x6:
              v[0xf] = (byte)(v[y] & 0x1);
              v[x] /*= v[y]*/ >>= 1;
              break;
            case 0x7:
            {
              var result = v[y] - v[x];
              v[x] = (byte)result;
              v[0xf] = result >= 0 ? True : False;
              break;
            }
            case 0xE:
              v[0xf] = (byte)(v[y] >> 7);
              v[x] /*= v[y]*/ <<= 1;
              break;
            default:
              throw new NotSupportedException($"Unknown opcode {opcode:X4}");
          }
          break;
        case 0x9:
          if (v[x] != v[y])
          {
            Cpu.Next();
          }
          break;
        case 0xA:
          Cpu.AddressRegister = nnn;
          break;
        case 0xB:
          Cpu.Jump((ushort)(v[0] + nnn));
          break;
        case 0xC:
          v[x] = (byte)(rnd.Next(256) & nn);
          break;
        case 0xD:
        {
          var height = opcode & 0xF;
          // ReSharper disable once InconsistentNaming
          const int width = 8;
          x = v[x];
          y = v[y];
          v[0xF] = 0;
          for (var dy = 0; dy < height; dy++)
          {
            var row = Memory[Cpu.AddressRegister + dy];
            for (var dx = 0; dx < width; dx++)
            {
              if ((row & (0x80 >> dx)) != 0)
              {
                if (!Display.TogglePixel(x + dx, y + dy))
                {
                  v[0xF] = 1;
                }
              }
            }
          }

          break;
        }
        case 0xE:
        {
          var action = (byte)opcode;
          if (action == 0x9E)
          {
            if (Keys[v[x]]) { Cpu.Next(); }
          }
          else if (action == 0xA1)
          {
            if (!Keys[v[x]]) { Cpu.Next(); }
          }
          else
          {
            throw new NotSupportedException($"Unknown opcode {opcode:X4}");
          }
          break;
        }
        case 0xF:
        {
          var action = (byte)opcode;
          switch (action)
          {
            case 0x07:
              v[x] = DelayTimer;
              break;
            case 0x0A:
            {
              var keyPressed = false;
              for (byte key = 0; key < Keys.Length; key++)
              {
                if (Keys[key])
                {
                  v[x] = key;
                  keyPressed = true;
                  break;
                }
              }
              if (!keyPressed)
              {
                Cpu.Retry();
              }
              break;
            }
            case 0x15:
              DelayTimer = v[x];
              break;
            case 0x18:
              SoundTimer = v[x];
              break;
            case 0x1E:
              var newValue = Cpu.AddressRegister + v[x];
              Cpu.AddressRegister = (ushort)newValue;
              v[0xF] = newValue > 0xFFF ? True : False;
              break;
            case 0x29:
              Cpu.AddressRegister = (ushort)(v[x] * 5);
              break;
            case 0x33:
            {
              Memory[Cpu.AddressRegister + 0] = (byte)(v[x] / 100);
              Memory[Cpu.AddressRegister + 1] = (byte)(v[x] / 10 % 10);
              Memory[Cpu.AddressRegister + 2] = (byte)(v[x] % 10);
              break;
            }
            case 0x55:
              for (var i = 0; i <= x; i++)
              {
                Memory[Cpu.AddressRegister++] = v[i];
              }
              break;
            case 0x65:
              for (var i = 0; i <= x; i++)
              {
                v[i] = Memory[Cpu.AddressRegister++];
              }
              break;
            default:
              throw new NotSupportedException($"Unknown opcode {opcode:X4}");
          }
          break;
        }
        default:
          throw new NotSupportedException($"Unknown opcode {opcode:X4}");
      }
    }
  }
}