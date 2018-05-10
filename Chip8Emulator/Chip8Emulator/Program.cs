using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chip8Emulator
{
  internal static class Program
  {
    private static async Task Main(string[] args)
    {
      if (args.Length == 0)
      {
        return;
      }

      var machine = new Machine();
      machine.LoadProgram(args[0]);

      var cts = new CancellationTokenSource();
      var form = new Screen(machine);
      var programTask = Task.Run(() => machine.ExecuteProgram(cts.Token), cts.Token);
      Application.Run(form);
      cts.Cancel();
      await programTask;
    }
  }
}