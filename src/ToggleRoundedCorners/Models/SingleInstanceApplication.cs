namespace Dawn.Apps.ToggleRoundedCorners.Models;

using System.IO;
using System.Text;
using System.Windows;
using Vanara.PInvoke;

public class SingleInstanceApplication : Application
{
    private static void AttachToParent() => Kernel32.AttachConsole(Kernel32.ATTACH_PARENT_PROCESS);

    protected SingleInstanceApplication()
    {
        AttachToParent();
        Console.WriteLine();
        
        #if !DEBUG
        var name = System.Windows.Forms.Application.ProductName!;
        name = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
        
        var mutex = new Mutex(true, name, out var createdNew);
        
        if (createdNew)
        {
            GC.KeepAlive(mutex);
            
            AppDomain.CurrentDomain.ProcessExit += (_, _) => mutex.Dispose();
            return;
        }
        mutex.Dispose();
        
        Console.Error.WriteLine("Another instance of this application is already running.");

        Shutdown(1);
        #endif
    }
}