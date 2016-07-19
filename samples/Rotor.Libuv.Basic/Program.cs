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
            var loop = new UvLoopHandle();
            var libuv = new Binding();
            loop.Init(libuv);

            int ctr = 10;
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

            var timer = new UvTimerHandle();
            timer.Init(loop, () =>
            {
                Console.WriteLine("Timeout");
            }, null);

            idle.Start();
            timer.Start(500, 2000);

            loop.Run();
        }
    }
}
