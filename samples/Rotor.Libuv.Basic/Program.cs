using System;
using System.Threading;
using System.Threading.Tasks;
using Rotor.Libuv;
using Rotor.Libuv.Networking;

namespace Rotor.Libuv.Basic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //Create a libuv wrapper and a loop object
            var loop = new UvLoopHandle();
            var libuv = new Binding();
            loop.Init(libuv);

            //Register an `idle` handle, which runs on every loop iteration
            //This particular impl is like `while (ctr > 0) { ... }`
            int ctr = 3;
            var idle = new UvIdleHandle();
            idle.Init(loop, () => 
            {
                //Yeh, man! Shared mutable state!
                if (ctr > 0)
                {
                    Console.WriteLine("Hello {0}", ctr);
                    ctr -= 1;

                    Thread.Sleep(500);
                }
                else
                {
                    loop.Stop();
                }
            }, null);

            idle.Start();
            loop.Run();
        }
    }
}
