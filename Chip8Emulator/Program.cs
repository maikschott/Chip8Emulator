using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chip8Emulator
{
  internal static class Program
  {
    private static void Main(string[] args)
    {
      if (args.Length == 0)
      {
        return;
      }

      var machine = new Machine();
      machine.LoadProgram(args[0]);

      var cts = new CancellationTokenSource();
      var programTask = Task.Run(() => machine.ExecuteProgram(cts.Token), cts.Token);
      var form = new Screen(machine, programTask, cts);
      Application.Run(form);
    }
  }
}