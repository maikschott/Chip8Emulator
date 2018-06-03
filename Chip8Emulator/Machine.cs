using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Chip8Emulator
{
  public class Machine
  {
    public const int EmulationSpeed = 1000; // Hertz
    public const int IoFrequency = 60; // Hertz
    public const int MemorySize = 4096;
    private const byte False = 0;
    private const byte True = 1;
    public static readonly TimeSpan CpuCycleDuration = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / EmulationSpeed);
    public static readonly TimeSpan IoCycleDuration = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / IoFrequency);

    private readonly Dictionary<byte, OpcodeContext> opcodeGroups;
    private readonly Random rnd;
    private readonly Stopwatch timer = new Stopwatch();

    public Machine()
    {
      Memory = new byte[MemorySize];
      Cpu = new CpuState();
      GraphicsUnit = new GraphicsUnit();
      Keys = new bool[16];
      rnd = new Random();

      Array.Copy(GraphicsUnit.FontSet, Memory, GraphicsUnit.FontSet.Length);

      opcodeGroups = new Dictionary<byte, OpcodeContext>
      {
        [0x0] = OpcodeSystem,
        [0x1] = OpcodeJump,
        [0x2] = OpcodeCall,
        [0x3] = OpcodeSkipIfEqual,
        [0x4] = OpcodeSkipIfNotEqual,
        [0x5] = OpcodeSkipIfVEqual,
        [0x6] = OpcodeLoad,
        [0x7] = OpcodeAdd,
        [0x8] = OpcodeArithmetic,
        [0x9] = OpcodeSkipIfVNotEqual,
        [0xA] = OpcodeLoadAddr,
        [0xB] = OpcodeJumpRelative,
        [0xC] = OpcodeRandom,
        [0xD] = OpcodeDisplay,
        [0xE] = OpcodeKeys,
        [0xF] = OpcodeMisc
      };
    }

    public byte[] Memory { get; }

    public CpuState Cpu { get; private set; }

    public GraphicsUnit GraphicsUnit { get; }

    public byte DelayTimer { get; set; }

    public byte SoundTimer { get; set; }

    public bool[] Keys { get; set; }

    public bool Running { get; set; }

    public void Reset()
    {
      Cpu = new CpuState();
      GraphicsUnit.ClearScreen();
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
      Running = true;
      timer.Restart();

      while (!cancellationToken.IsCancellationRequested && Running)
      {
        var elapsedTime = timer.Elapsed;

        ProcessCpuCycle();

        while (elapsedTime >= IoCycleDuration)
        {
          ProcessIoCycle();

          elapsedTime -= IoCycleDuration;
          timer.Restart();
        }

        Thread.Sleep(CpuCycleDuration);
      }

      Console.WriteLine("--END--");
    }

    public void ProcessCpuCycle()
    {
      PrintState();
      EmulateCycle();
    }

    public void ProcessIoCycle()
    {
      if (DelayTimer > 0) { DelayTimer--; }
      if (SoundTimer > 0)
      {
        if (--SoundTimer == 0)
        {
          Console.Beep(800, 50);
        }
      }

      GraphicsUnit.Draw();
    }

    [DllImport("user32.dll")]
    public static extern ushort GetAsyncKeyState(short nVirtKey);

    public static bool IsKeyPressed(ConsoleKey key)
    {
      const ushort KeyDown = 0x8000;
      return (GetAsyncKeyState((short)key) & KeyDown) == KeyDown;
    }

    [Conditional("DEBUG")]
    private void PrintState()
    {
      var sb = new StringBuilder(118);
      sb.Append("I=");
      sb.Append(Cpu.AddressRegister.ToString("X4"));
      for (int i = 0; i < Cpu.Registers.Length; i++)
      {
        sb.Append(", V");
        sb.Append(i.ToString("X1"));
        sb.Append('=');
        sb.Append(Cpu.Registers[i].ToString("X2"));
      }

      Console.Title = sb.ToString();
    }

    private void EmulateCycle()
    {
      var opcode = (ushort)((Memory[Cpu.Pc] << 8) | Memory[Cpu.Pc + 1]);
      Cpu.Next();

      var x = (byte)((opcode >> 8) & 0xF);
      var y = (byte)((opcode >> 4) & 0xF);
      var nn = (byte)opcode;
      var nnn = (ushort)(opcode & 0x0FFF);
      var v = Cpu.Registers;

      var opcodeHandler = opcodeGroups[(byte)(opcode >> 12)];
      opcodeHandler(x, y, nn, nnn, v);
    }

    private void OpcodeSystem(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      if ((nn & 0xF0) == 0xC0)
      {
        var pixels = nn & 0xF;
        Debug($"SCD {pixels}");
        GraphicsUnit.ScrollDown(pixels);
        return;
      }

      switch (nn)
      {
        case 0xE0:
          Debug("CLS");
          GraphicsUnit.ClearScreen();
          break;
        case 0xEE:
          Debug("RET");
          Cpu.Jump(Cpu.Callstack.Pop());
          break;
        case 0xFB:
          Debug("SCR");
          GraphicsUnit.ScrollRight(4);
          break;
        case 0xFC:
          Debug("SCL");
          GraphicsUnit.ScrollLeft(4);
          break;
        case 0xFD:
          Debug("EXIT");
          Running = false;
          break;
        case 0xFE:
          Debug("LOW");
          GraphicsUnit.HighRes = false;
          break;
        case 0xFF:
          Debug("HIGH");
          GraphicsUnit.HighRes = true;
          break;
        default:
          Debug($"Unknown opcode 0{nnn:X3}");
          break;
      }
    }

    private void OpcodeJump(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"JMP {nnn:X3}");
      Cpu.Jump(nnn);
    }

    private void OpcodeCall(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"CALL {nnn:X3}");
      Cpu.Callstack.Push(Cpu.Pc);
      Cpu.Jump(nnn);
    }

    private void OpcodeSkipIfEqual(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"SE V{x:X}, {nn:X2}");
      if (v[x] == nn) { Cpu.Next(); }
    }

    private void OpcodeSkipIfNotEqual(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"SNE V{x:X}, {nn:X2}");
      if (v[x] != nn) { Cpu.Next(); }
    }

    private void OpcodeSkipIfVEqual(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"SE V{x:X}, V{y:X}");
      if (v[x] == v[y]) { Cpu.Next(); }
    }

    private void OpcodeLoad(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"LD V{x:X}, {nn:X2}");
      v[x] = nn;
    }

    private void OpcodeAdd(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"ADD V{x:X}, {nn:X2}");
      v[x] += nn;
    }

    private void OpcodeArithmetic(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      switch (nn & 0xF)
      {
        case 0x0:
          Debug($"LD V{x:X}, V{y:X}");
          v[x] = v[y];
          break;
        case 0x1:
          Debug($"OR V{x:X}, V{y:X}");
          v[x] |= v[y];
          break;
        case 0x2:
          Debug($"AND V{x:X}, V{y:X}");
          v[x] &= v[y];
          break;
        case 0x3:
          Debug($"XOR V{x:X}, V{y:X}");
          v[x] ^= v[y];
          break;
        case 0x4:
        {
          Debug($"ADD V{x:X}, V{y:X}");
          var result = v[x] + v[y];
          v[x] = (byte)result;
          v[0xf] = result > 255 ? True : False;
          break;
        }
        case 0x5:
        {
          Debug($"SUB V{x:X}, V{y:X}");
          var result = v[x] - v[y];
          v[x] = (byte)result;
          v[0xf] = result >= 0 ? True : False;
          break;
        }
        case 0x6:
          Debug($"SHR V{x:X}, 1");
          v[0xf] = (byte)(v[y] & 0x1);
          v[x] /*= v[y]*/ >>= 1;
          break;
        case 0x7:
        {
          Debug($"SUBN V{x:X}, V{y:X}");
          var result = v[y] - v[x];
          v[x] = (byte)result;
          v[0xf] = result >= 0 ? True : False;
          break;
        }
        case 0xE:
          Debug($"SHL V{x:X}, 1");
          v[0xf] = (byte)(v[y] >> 7);
          v[x] /*= v[y]*/ <<= 1;
          break;
        default:
          Debug($"Unknown opcode 8{nnn:X3}");
          break;
      }
    }

    private void OpcodeSkipIfVNotEqual(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"SNE V{x:X}, V{y:X}");
      if (v[x] != v[y]) { Cpu.Next(); }
    }

    private void OpcodeLoadAddr(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"LD I, {nnn:X3}");
      Cpu.AddressRegister = nnn;
    }

    private void OpcodeJumpRelative(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"JMP V0, {nnn:X3}");
      Cpu.Jump((ushort)(v[0] + nnn));
    }

    private void OpcodeRandom(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      Debug($"RND V{x:X}, {nn:X2}");
      v[x] = (byte)(rnd.Next(256) & nn);
    }

    private void OpcodeDisplay(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      var height = nn & 0xF;
      Debug($"DRW V{x:X}, V{y:X}, {height}");

      var width = 8;
      var msb = 0x80;
      if (height == 0 && GraphicsUnit.HighRes)
      {
        height = 16;
        width = 16;
        msb = 0x8000;
      }

      x = v[x];
      y = v[y];
      v[0xF] = 0;
      for (var dy = 0; dy < height; dy++)
      {
        var row = width == 8 ? Memory[Cpu.AddressRegister + dy] : (Memory[Cpu.AddressRegister + dy * 2] << 8) | Memory[Cpu.AddressRegister + dy * 2 + 1];
        for (var dx = 0; dx < width; dx++)
        {
          var mask = msb >> dx;
          if ((row & mask) != 0)
          {
            if (!GraphicsUnit.TogglePixel(x + dx, y + dy))
            {
              v[0xF] = 1;
            }
          }
        }
      }
    }

    private void OpcodeKeys(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      if (nn == 0x9E)
      {
        Debug($"SKP V{x:X}");
        if (Keys[v[x]]) { Cpu.Next(); }
      }
      else if (nn == 0xA1)
      {
        Debug($"SKNP V{x:X}");
        if (!Keys[v[x]]) { Cpu.Next(); }
      }
      else
      {
        Debug($"Unknown opcode E{nnn:X3}");
      }
    }

    private void OpcodeMisc(byte x, byte y, byte nn, ushort nnn, byte[] v)
    {
      switch (nn)
      {
        case 0x07:
          Debug($"LD V{x:X}, DT");
          v[x] = DelayTimer;
          break;
        case 0x0A:
        {
          Debug($"LD V{x:X}, K");
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
          Debug($"LD DT, V{x:X}");
          DelayTimer = v[x];
          break;
        case 0x18:
          Debug($"LD ST, V{x:X}");
          SoundTimer = v[x];
          break;
        case 0x1E:
          Debug($"ADD I, V{x:X}");
          var newValue = Cpu.AddressRegister + v[x];
          Cpu.AddressRegister = (ushort)newValue;
          v[0xF] = newValue > 0xFFF ? True : False;
          break;
        case 0x29:
          Debug($"LD F, V{x:X}");
          Cpu.AddressRegister = (ushort)(v[x] * 5);
          break;
        case 0x30:
          Debug($"LD HF, V{x:X}");
          Cpu.AddressRegister = (ushort)(v[x] * 10);
          break;
        case 0x33:
        {
          Debug($"LD B, V{x:X}");
          Memory[Cpu.AddressRegister + 0] = (byte)(v[x] / 100);
          Memory[Cpu.AddressRegister + 1] = (byte)(v[x] / 10 % 10);
          Memory[Cpu.AddressRegister + 2] = (byte)(v[x] % 10);
          break;
        }
        case 0x55:
          Debug($"LD [I], V{x:X}");
          for (var i = 0; i <= x; i++)
          {
            Memory[Cpu.AddressRegister++] = v[i];
          }
          break;
        case 0x65:
          Debug($"LD V{x:X}, [I]");
          for (var i = 0; i <= x; i++)
          {
            v[i] = Memory[Cpu.AddressRegister++];
          }
          break;
        case 0x75:
          Debug($"LD R, V{x:X}");
          Cpu.UserFlags = v[x];
          break;
        case 0x85:
          Debug($"LD V{x:X}, R");
          v[x] = Cpu.UserFlags;
          break;
        default:
          Debug($"Unknown opcode F{nnn:X3}");
          break;
      }
    }

    [Conditional("DEBUG")]
    private void Debug(string command)
    {
      Console.WriteLine($"{Cpu.Pc - 2:X4}: {command}");
    }

    private delegate void OpcodeContext(byte x, byte y, byte nn, ushort nnn, byte[] v);
  }
}