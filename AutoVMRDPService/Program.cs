using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace AutoVMRDPService
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<VMRDPService>(s =>
                {
                    s.ConstructUsing(name => new VMRDPService());
                    s.WhenStarted(vmrdp => vmrdp.Start());
                    s.WhenStopped(vmrdp => vmrdp.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("Manages automatically starting and stopping a VM upon RDP connection");
                x.SetDisplayName("VM/RDP Management");
                x.SetServiceName("vmrdp");
            });
        }
    }
}
